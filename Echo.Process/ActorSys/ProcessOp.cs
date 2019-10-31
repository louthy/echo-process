﻿using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using static LanguageExt.Prelude;

namespace Echo
{
    abstract class ProcessOp
    {
        public abstract Unit Run(ProcessId pid);

        public static Unit IO(Action op)
        {
            if (Process.InMessageLoop && ActorContext.System(default(SystemName)).Settings.TransactionalIO)
            {
                ActorContext.Request.SetOps(ActorContext.Request.Ops.IO(op));
                return unit;
            }
            else
            {
                op();
                return unit;
            }
        }

        public static Unit IO(Func<Unit> op)
        {
            if (Process.InMessageLoop && ActorContext.System(default(SystemName)).Settings.TransactionalIO)
            {
                ActorContext.Request.SetOps(ActorContext.Request.Ops.IO(op));
                return unit;
            }
            else
            {
                return op();
            }
        }
    }

    class ProcessOpTransaction
    {
        public readonly ProcessId ProcessId;
        public readonly Que<ProcessOp> Ops;
        public readonly Option<HashMap<string, object>> Settings;

        public ProcessOpTransaction(ProcessId pid, Que<ProcessOp> ops, Option<HashMap<string, object>> settings)
        {
            ProcessId = pid;
            Ops = ops;
            Settings = settings;
        }

        public ProcessOpTransaction Write(object value, string name, string prop, ProcessFlags flags)
        {
            var op = new WriteConfigOp(value, name, prop, flags);
            var settings = Settings.IfNone(HashMap<string, object>()).AddOrUpdate($"{name}@{prop}", value);
            return new ProcessOpTransaction(ProcessId, Ops.Enqueue(op), settings);
        }

        public ProcessOpTransaction Clear(string name, string prop, ProcessFlags flags)
        {
            var op = new ClearConfigOp(name, prop, flags);
            var settings = Settings.IfNone(HashMap<string, object>()).Remove($"{name}@{prop}");
            return new ProcessOpTransaction(ProcessId, Ops.Enqueue(op), settings);
        }

        public ProcessOpTransaction ClearAll(ProcessFlags flags)
        {
            var op = new ClearAllOp(flags);
            var settings = Settings.IfNone(HashMap<string, object>()).Clear();
            return new ProcessOpTransaction(ProcessId, Ops.Enqueue(op), settings);
        }

        public ProcessOpTransaction IO(Func<Unit> op) =>
            new ProcessOpTransaction(ProcessId, Ops.Enqueue(new IOOp(op)), Settings);

        public ProcessOpTransaction IO(Action op) =>
            new ProcessOpTransaction(ProcessId, Ops.Enqueue(new IOOp(op)), Settings);

        public T Read<T>(string name, string prop, ProcessFlags flags, T defaultValue)
        {
            var val = Settings.IfNone(HashMap<string, object>()).Find($"{name}@{prop}");
            if (val.IsSome) return val.Map(x => (T)x).IfNone(defaultValue);
            return ActorContext.System(ProcessId).Settings.GetProcessSetting<T>(ProcessId, name, prop, flags).IfNone(defaultValue);
        }

        public static ProcessOpTransaction Start(ProcessId pid) =>
            new ProcessOpTransaction(pid, Que<ProcessOp>.Empty, ActorContext.System(pid).Settings.GetProcessSettingsOverrides(pid));

        public ProcessOpTransaction Run()
        {
            Run(Ops);
            return Start(ProcessId);
        }

        Unit Run(Que<ProcessOp> ops)
        {
            while (ops.Count > 0)
            {
                ops.Peek().Run(ProcessId);
                ops = ops.Dequeue();
            }
            return unit;
        }
    }

    class IOOp : ProcessOp
    {
        public readonly Func<Unit> Op;

        public IOOp(Func<Unit> op)
        {
            Op = op;
        }

        public IOOp(Action op)
        {
            Op = fun(op);
        }

        public override Unit Run(ProcessId pid) => Op();
    }

    class WriteConfigOp : ProcessOp
    {
        public readonly object Value;
        public readonly string Name;
        public readonly string Prop;
        public readonly ProcessFlags Flags;

        public WriteConfigOp(object value, string name, string prop, ProcessFlags flags)
        {
            Value = value;
            Name = name;
            Prop = prop;
            Flags = flags;
        }

        public override Unit Run(ProcessId pid) =>
            ActorContext.System(pid).Settings.WriteSettingOverride(ActorInboxCommon.ClusterSettingsKey(pid), Value, Name, Prop, Flags);
    }

    class ClearConfigOp : ProcessOp
    {
        public readonly string Name;
        public readonly string Prop;
        public readonly ProcessFlags Flags;

        public ClearConfigOp(string name, string prop, ProcessFlags flags)
        {
            Name = name;
            Prop = prop;
            Flags = flags;
        }

        public override Unit Run(ProcessId pid) =>
            ActorContext.System(pid).Settings.ClearSettingOverride(ActorInboxCommon.ClusterSettingsKey(pid), Name, Prop, Flags);
    }

    class ClearAllOp : ProcessOp
    {
        public readonly ProcessFlags Flags;

        public ClearAllOp(ProcessFlags flags)
        {
            Flags = flags;
        }

        public override Unit Run(ProcessId pid) =>
            ActorContext.System(pid).Settings.ClearSettingsOverride(ActorInboxCommon.ClusterSettingsKey(pid), Flags);
    }
}
