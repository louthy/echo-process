// LanguageExt.Process.js
// The MIT License (MIT)
// Copyright (c) 2014-2017 Paul Louth
// https://github.com/louthy/language-ext/blob/master/LICENSE.md

var unit = "(unit)";

if (!window.location.origin) {
    window.location.origin = window.location.protocol + "//"
        + window.location.hostname
        + (window.location.port ? ':' + window.location.port : '');
}

var failwith = function (err) {
    console.error(err);
    throw err;
};

var PID = function (path) {
    return {
        Path: path
    };
};

var Some = function (value) {
    return [].push(value);
}
var None = [];
var isSome = function (value) {
    return Object.prototype.toString.call(value) === '[object Array]' && value.length === 1;
}
var isNone = function (value) {
    return !isSome(value);
}
var match = function (value, Some, None) {
    return typeof value !== "undefined" && value !== null && isSome(value)
        ? Some(value[0])
        : None();
}

var ActorResponse = function (message, replyTo, replyFrom, requestId, isFaulted) {
    return {
        ReplyTo: PID(replyTo),
        Message: message,
        ReplyFrom: PID(replyFrom),
        RequestId: requestId,
        IsFaulted: isFaulted
    };
};

var Process = (function () {

    var messageId = 0;

    var rnd2 = function (count) {
        var buffer = [];
        for (var i = 0; i < count; i++)
            buffer.push(Math.floor(parseInt(Math.random() * 255)));
        return buffer.join("");
    };

    var rnd = function (count) {
        var crypto = window.crypto || window.msCrypto;
        if (!crypto) return rnd2(count);
        var buffer = new Uint8Array(count || 16);
        crypto.getRandomValues(buffer);
        return buffer.join("");
    };

    function isEmpty(obj) {
        for (var prop in obj) {
            if (obj.hasOwnProperty(prop))
                return false;
        }
        return JSON.stringify(obj) === JSON.stringify({});
    }

    var Connect = "procsys:conn";
    var Disconnect = function (id) {
        return "procsys:diss|" + id;
    };
    var Ping = function (id) {
        return "procsys:ping|" + id;
    };
    var Tell = function (id, msgid, to, sender, msg) {
        return "procsys:tell|" + id + "|" + msgid + "|" + to + "|" + sender + "|" + JSON.stringify(msg);
    };
    var Ask = function (id, msgid, to, sender, msg) {
        return "procsys:ask|" + id + "|" + msgid + "|" + to + "|" + sender + "|" + JSON.stringify(msg);
    };
    var Subscribe = function (id, pub, sub) {
        return "procsys:sub|" + id + "|" + pub + "|" + sub;
    };
    var Unsubscribe = function (id, pub, sub) {
        return "procsys:usub|" + id + "|" + pub + "|" + sub;
    };

    var withContext = function (ctx, f) {
        var savedContext = context;

        if (!ctx) {
            ctx = {
                parent: User,
                self: User,
                sender: NoSender,
                currentMsg: null,
                currentReq: null
            };
        }
        context = ctx;
        var res = null;
        try {
            res = f(context);
        }
        catch (e) {
            error(e + "(" + context.self + ")", ctx.self, ctx.currentMsg, ctx.sender);
        }
        context = savedContext;
        return res;
    };

    var secure = window.location.protocol === "https:";
    var protocol = secure ? "wss://" : "ws://";
    var uri = protocol + window.location.host + "/process-sys";
    var connectionId = "";
    var socket = null;
    var doneFn = function () { };
    var failFn = function (e) { };
    var lastPong = new Date();
    var requests = {};

    var actor = { "/": { children: {} } };
    var ignore = function () { };
    var id = function (id) { return id; };
    var publish = null;
    var inboxStart = function (pid, parent, setup, inbox, stateless, shutdown) {

        ctx = {
            self: pid,
            parent: parent,
            sender: NoSender,
            currentMsg: null,
            currentReq: null
        };

        var process = {
            pid: pid,
            state: null,
            setup: setup,
            inbox: inbox,
            stateless: stateless,
            children: {},
            subs: {},
            obs: {},
            shutdown: shutdown
        };

        actor[parent].children[pid] = process;
        actor[pid] = process;

        process.state = withContext(ctx, function () {
            return setup();
        });

        return process;
    };
    var Root = "/root-js";
    var System = "/root-js/system";
    var DeadLetters = "/root-js/system/dead-letters";
    var Errors = "/root-js/system/errors";
    var User = "/root-js/user";
    var NoSender = "/no-sender";
    var root = inboxStart(Root, "/", ignore, function (msg) { publish(msg); }, true);
    var system = inboxStart(System, Root, ignore, function (msg) { publish(msg); }, true);
    var user = inboxStart(User, Root, ignore, function (msg) { publish(msg); }, true);
    var noSender = inboxStart(NoSender, "/", ignore, function (msg) { publish(msg); }, true);
    var deadLetters = inboxStart(DeadLetters, System, ignore, function (msg) { publish(msg); }, true);
    var errors = inboxStart(Errors, System, ignore, function (msg) { publish(msg); }, true);
    var subscribeId = 1;
    var context = null;
    var keepAlive = null;

    var inloop = function () {
        return context !== null;
    };

    var isLocal = function (pid) {
        if (typeof pid === "undefined") failwith("isLocal: 'pid' not defined");
        return pid.indexOf(Root) === 0;
    };

    var getSender = function (sender) {
        return sender || (inloop() ? context.self : NoSender);
    };

    var spawn = function (name, setup, inbox, shutdown) {
        if (typeof name === "undefined") failwith("spawn: 'name' not defined");
        var stateless = false;
        if (arguments.length === 2) {
            inbox = setup;
            setup = function () { return {}; };
            stateless = true;
        }
        else {
            if (typeof setup !== "function") {
                var val = setup;
                setup = function () { return val; };
            }
        }

        return withContext(context, function () {
            var pid = context.self + "/" + name;
            inboxStart(pid, context.self, setup, inbox, stateless, shutdown);
            return pid;
        });
    };

    var tell = function (pid, msg, sender) {
        if (typeof pid === "undefined") failwith("tell: 'pid' not defined");
        if (typeof msg === "undefined") failwith("tell: 'msg' not defined");
        if (msg == unit) msg = {};

        var ctx = {
            isAsk: false,
            self: pid,
            sender: getSender(sender),
            currentMsg: msg,
            currentReq: null
        };
        postMessage(
            JSON.stringify({ processjs: "tell", pid: pid, msg: msg, ctx: ctx }),
            window.location.origin
        );
    };

    var tellDelay = function (pid, msg, delay) {
        if (typeof pid === "undefined") failwith("tellDelay: 'pid' not defined");
        if (typeof msg === "undefined") failwith("tellDelay: 'msg' not defined");
        if (typeof delay === "undefined") failwith("tellDelay: 'delay' not defined");
        var self = context.self;
        return setTimeout(function () { tell(pid, msg, self); }, delay);
    };

    var askInternal = function (pid, msg, ctx) {
        var p = actor[pid];
        if (!p || !p.inbox) {
            if (isLocal(pid)) {
                failwith("Process doesn't exist " + pid);
                return null;
            }
            else {
                messageId++;
                socket.send(Ask(connectionId, messageId, pid, ctx.sender, msg));

                var thunk = {
                    doneFn: function (msg) {
                    },
                    failFn: function (msg) {
                    },
                    done: function (f) {
                        this.doneFn = f;
                        return this;
                    },
                    fail: function (f) {
                        this.failFn = f;
                        return this;
                    },
                    ctx: ctx
                };

                requests[messageId] = thunk;
                return thunk;
            }
        }
        else {
            // TODO: This isn't ideal, you could 'jump ahead' of the queue where an ask arrives 
            //       before a postMessaged 'tell'.  In reality this may not be a huge issue because
            //       posting to an actor is always supposed to be asynchronous, so you don't know 
            //       what order a message arrives at the inbox (and therefore shouldn't rely on 
            //       the send order).  Also, this allows for blocking 'ask', which is difficult to
            //       achieve by other means.
            return withContext(ctx, function () {
                context.reply = null;
                if (p.stateless) {
                    var statea = p.inbox(msg);
                    if (typeof statea !== "undefined") p.state = statea;
                }
                else {
                    var stateb = p.inbox(p.state, msg);
                    if (typeof stateb !== "undefined") p.state = stateb;
                }
                return context.reply;
            });
        }
    };

    var ask = function (pid, msg) {
        if (typeof pid === "undefined") failwith("ask: 'pid' not defined");
        if (typeof msg === "undefined") failwith("ask: 'msg' not defined");
        if (msg == unit) msg = {};
        var ctx = {
            isAsk: true,
            self: pid,
            sender: getSender(),
            currentMsg: msg,
            currentReq: null
        };
        return askInternal(pid, msg, ctx);
    };

    var reply = function (msg) {
        if (typeof msg === "undefined") failwith("reply: 'msg' not defined");
        if (msg == unit) msg = {};
        context.reply = msg;
    };

    var subscribeAsync = function (pid) {
        if (typeof pid === "undefined") failwith("subscribeAsync: 'pid' not defined");
        var p = actor[pid];
        if (!p || !p.inbox) {
            if (isLocal(pid)) {
                failwith("Process doesn't exist " + pid);
            }
            else {
                failwith("'subscribe' is currently only available for intra-JS process calls.");
            }
        }

        var id = subscribeId;
        var onNext = function (msg) { };
        var onComplete = function () { };

        p.subs[id] = {
            next: function (msg) {
                if (isEmpty(msg)) msg = unit;
                onNext(msg);
            },
            done: function () { onComplete(); }
        };

        subscribeId++;

        return {
            unsubscribe: function () {
                onComplete();
                delete p.subs[id];
            },
            forall: function (f) { onNext = f; return this; },
            done: function (f) { onComplete = f; return this; }
        };
    };

    var subscribeSync = function (pid) {
        if (typeof pid === "undefined") failwith("subscribeSync: 'pid' not defined");
        var self = context.self;
        if (isLocal(pid)) {
            return subscribeAsync(pid).forall(function (msg) {
                if (isEmpty(msg)) msg = unit;
                tell(self, msg, pid);
            });
        }
        else {
            var id = subscribeId;
            var ctx = {
                unsubscribe: function () {
                    socket.send(Unsubscribe(connectionId, pid, self));
                    delete actor[self].obs[id];
                },
                next: function () { },
                done: function () { }
            };
            actor[self].obs[id] = ctx;
            subscribeId++;
            socket.send(Subscribe(connectionId, pid, self));
            return ctx;
        }
    };

    var subscribe = function (pid) {
        if (typeof pid === "undefined") failwith("subscribe: 'pid' not defined");
        return inloop()
            ? subscribeSync(pid)
            : subscribeAsync(pid);
    };

    var unsubscribe = function (ctx) {
        if (typeof ctx === "undefined") failwith("unsubscribe: 'ctx' not defined");
        if (!ctx || ctx.unsubscribe) return;
        ctx.unsubscribe();
    };

    publish = function (msg) {
        if (typeof msg === "undefined") failwith("publish: 'msg' not defined");
        if (msg == unit) msg = {};
        if (inloop()) {
            postMessage(
                JSON.stringify({ pid: context.self, msg: msg, processjs: "pub" }),
                window.location.origin
            );
        }
        else {
            failwith("'publish' can only be called from within a process");
        }
    };

    var kill = function (pid) {
        if (typeof pid === "undefined") failwith("kill: 'pid' not defined");
        if (arguments.length === 0) {
            if (inloop()) {
                pid = context.pid;
            }
            else {
                failwith("'kill' can only be called without arguments from within a process");
            }
        }

        var p = actor[pid];
        if ("undefined" === typeof p) {
            return;
        }
        var children = p.children;
        for (var i = 0; i < children.length; i++) {
            kill(children[i]);
        }

        if (typeof p.shutdown === "function") {
            p.shutdown(p.state);
        }

        for (var ob in p.obs) {
            p.obs[ob].unsubscribe();
        }
        p.obs = {};

        for (var x in p.subs) {
            var sub = p.subs[x];
            if (typeof sub.done === "function") {
                try {
                    sub.done();
                }
                catch (e) {
                    // Ignore.  We just want to stop other 
                    // subscribers being hurt by one bad egg.
                    error(e, pid + "/sub/done", null, null);
                }
            }
        }
        p.subs = {};

        delete actor[extractParent(pid)].children[pid];
        delete actor[pid];
    };

    var deadLetter = function (to, msg, sender) {
        tell(DeadLetters, { to: to, msg: msg, sender: getSender(sender) });
    };

    var error = function (e, to, msg, sender) {
        console.error(e);
        tell(Errors, { error: e, to: to, msg: msg, sender: getSender(sender) });
    };

    var receiveTell = function (data) {
        var p = actor[data.pid];
        if (!p || !p.inbox) {
            if (isLocal(data.pid)) {
                deadLetter(data.pid, data.msg, data.sender);
            }
            else {
                var msg = data.msg;
                if (msg == unit) {
                    msg = {};
                }
                socket.send(Tell(connectionId, ++messageId, data.pid, data.ctx.sender, msg));
            }
            return;
        }

        try {
            withContext(data.ctx, function () {
                if (p.stateless) {
                    var statea = p.inbox(data.msg);
                    if (typeof statea !== "undefined") p.state = statea;
                }
                else {
                    var stateb = p.inbox(p.state, data.msg);
                    if (typeof stateb !== "undefined") p.state = stateb;
                }
            });
        }
        catch (e) {
            error(e, data.pid, data.msg, data.sender);
            deadLetter(data.pid, data.msg, data.sender);
            p.state = p.setup();
        }
    };

    var receivePub = function (data) {
        var p = actor[data.pid];
        if (!p || !p.subs) {
            return;
        }

        for (var x in p.subs) {
            var sub = p.subs[x];
            if (typeof sub.next === "function") {
                try {
                    sub.next(data.msg);
                }
                catch (e) {
                    // Ignore.  We just want to stop other subscribers
                    // being hurt by one bad egg.
                    error(e, data.pid + "/pub", data.msg, null);
                }
            }
        }
    };

    var receive = function (event) {
        if (event.origin !== window.location.origin ||
            typeof event.data !== "string") {
            return;
        }
        var data = JSON.parse(event.data);

        if (!data.processjs,
            !data.pid ||
            !data.msg) {
            return;
        }
        if (isEmpty(data.msg)) {
            data.msg = unit;
        }
        switch (data.processjs) {
            case "tell": receiveTell(data); break;
            case "pub": receivePub(data); break;
        }
    };

    var disconnect = function () {
        socket.send(Disconnect(connectionId));
        socket.close();
    };

    var unload = function () {
        socket.send(Disconnect(connectionId));
    };

    var connect = function () {
        socket = new WebSocket(uri);

        socket.onopen = function (e) {
            socket.send(Connect);
            window.addEventListener("beforeunload", unload);
        };

        socket.onclose = function (e) {
            window.removeEventListener("beforeunload", unload);
        };

        socket.onmessage = function (e) {

            try {
                var obj = JSON.parse(e.data);

                switch (obj.tag) {
                    // Incoming ask
                    case "ask":
                        var ctx = {
                            self: obj.to,
                            sender: obj.sender,
                            replyTo: obj.replyTo,
                            requestId: obj.requestId,
                            currentMsg: obj.content,
                            isAsk: true
                        };
                        try {
                            var res = askInternal(obj.to, obj.content, ctx);
                            tell(obj.replyTo, ActorResponse(res, obj.replyTo, obj.to, obj.requestId, false), obj.to);
                        }
                        catch (e) {
                            tell(obj.replyTo, ActorResponse(e, obj.replyTo, obj.to, obj.requestId, true), obj.to);
                        }
                        break;

                    // Incoming tell
                    case "tell":
                        var ctx = {
                            self: obj.to,
                            sender: obj.sender,
                            replyTo: obj.replyTo,
                            currentMsg: obj.content,
                            requestId: 0,
                            isAsk: false
                        };
                        postMessage(
                            JSON.stringify({ pid: ctx.self, msg: ctx.currentMsg, ctx: ctx, processjs: "tell" }),
                            window.location.origin
                        );
                        break;

                    // Remote ask response
                    case "askr":
                        var req = requests[obj.mid];
                        if (!req) return;
                        var done = req.doneFn;
                        var fail = req.failFn;
                        var ctx = req.ctx;
                        delete requests[obj.mid];

                        withContext(ctx, function () {
                            if (typeof obj.done !== "undefined") {
                                done(obj.done);
                            }
                            else if (typeof obj.fail !== "undefined") {
                                fail(obj.fail);
                            }
                        });
                        break;

                    // Pong (reply of ping)
                    case "pong":
                        lastPong = new Date();
                        if (obj.status === "False") {
                            connectionId = "";
                            socket.send(Connect);
                        }
                        break;

                    // Disconnected
                    case "disc":
                        connectionId = obj.id === connectionId ? "" : connectionId;
                        if (keepAlive) {
                            clearInterval(keepAlive);
                            keepAlive = null;
                        }
                        break;

                    // Connected
                    case "conn":
                        connectionId = obj.id === "" ? connectionId : obj.id;

                        if (keepAlive) {
                            clearInterval(keepAlive);
                        }
                        if (connectionId !== "") {
                            keepAlive = setInterval(function () {
                                socket.send(Ping(connectionId));
                            }, 5000);
                        }

                        doneFn();
                        break;
                }
            }
            catch (e) {
                console.error(e);
            }
        };

        socket.onerror = function (e) {
            console.error(e.data);
        };

        return {
            done: function (f) {
                doneFn = f;
                return this;
            },
            fail: function (f) {
                failFn = f;
                return this;
            }
        };
    };

    var isAsk = function () {
        return context
            ? context.isAsk
            : false;
    };

    var isTell = function () {
        return !isAsk;
    };

    var formatItem = function (msg) {
        return "<div class='process-log-msg-row'>" +
            "<div class='process-log-row process-log-row" + msg.TypeDisplay + "'>" +
            "<div class='log-time'>" + msg.DateDisplay + "</div>" +
            "<div class='log-type'>" + msg.TypeDisplay + "</div>" +
            match(msg.Message,
                function (value) { return "<div class='log-msg'>" + value + "</div>"; },
                function () { return "" }) +
            "</div>" +
            match(msg.Exception,
                function (value) { return "<div class='process-log-row testbed-log-rowError'><div class='log-ex-msg'>" + JSON.stringify(value, null, 4) + "</div></div>"; },
                function () { return "" }) +
            "</div>";
    };

    var log = {
        paused: false,
        tell: function (type, msg) {
            if (typeof type === "undefined") failwith("Log.tell: 'type' not defined");
            if (typeof msg === "undefined") failwith("Log.tell: 'msg' not defined");
            tell("/root/user/process-log", {
                Type: type,
                Message: msg
            });
        },
        tellInfo: function (msg) { this.tell(1, msg); },
        tellWarn: function (msg) { this.tell(2, msg); },
        tellError: function (msg) { this.tell(12, msg); },
        tellDebug: function (msg) { this.tell(16, msg); },
        injectCss: function () {
            var css = ".process-log-row{font-family:Calibri,Droid Sans,Candara,Segoe,'Segoe UI',Optima,Arial,sans-serif;font-size:10pt;border-left:8px solid #fff;background-color:#fff;box-shadow:2px 2px 2px #aaa;padding:4px}.process-log-rowInfo{border-color:#c1eaaf}.process-log-rowWarn{border-color:#ffea99}.process-log-rowError{border-color:#e29191}.process-log-rowDebug{border-color:#a1bacc}.process-item{width:400px;margin:10px 5px 0;padding:15px;display:inline-block;background-color:#f0f0f0;vertical-align:top;min-height:15px;border-radius:5px;box-shadow:5px 5px 5px #aaa}.process-log-row .log-ex-msg,.process-log-row .log-ex-stack,.process-log-row .log-msg,.process-log-row .log-time,.process-log-row .log-type{display:inline-block;padding:2px}.process input{margin-right:4px;margin-bottom:4px}.process-log-row .log-time{width:75px}.process-log-row .log-type{width:40px}.process-log-row .log-msg{width:auto} .log-ex-msg{white-space: pre-wrap;}";
            var style = document.createElement("style");
            style.type = "text/css";
            style.innerHTML = css;
            document.getElementsByTagName("head")[0].appendChild(style);
        },
        updateViewWithItems: function (items) {
            if (items && items.length > 0) {
                var view = [];
                for (var i = items.length - 1; i >= 0; i--) {
                    view.push(formatItem(items[i]));
                }
                document.getElementById(this.id).innerHTML = view.join("");
            }
        },
        refresh: function () {
            var self = this;
            Process.ask("/root/user/process-log", unit)
                .done(function (items) { self.updateViewWithItems(items); });
        },
        view: function (id, viewSize) {
            var self = this;
            this.injectCss();
            if (!id) failwith("Log.view: 'id' not defined");
            this.id = id;
            this.pid = Process.spawn("process-log",
                function () {
                    Process.subscribe("/root/user/process-log");
                    return [];
                },
                function (state, msg) {
                    if (!self.paused) { 
                        state.unshift(msg);
                        $("#" + id).prepend(formatItem(msg));
                        if (state.length > (viewSize || 50)) {
                            state.pop();
                            $('.process-log-msg-row:last').remove();
                        }
                        return state;
                    }
                }
            );
            return this.pid;
        },
        pause: function (onPaused) {
            if (this.paused) return;
            togglePause(onPaused);
        },
        resume: function (onResumed) {
            if (!this.paused) return;
            togglePause(onResumed);
        },
        togglePause: function(onPaused, onResumed) { 
            if (!this.pid || !this.id) return false;
            this.paused = !this.paused;
            if (this.paused && onPaused) onPaused();
            if (!this.paused && onResumed) onResumed();
            var self = this;
            if (!this.paused) this.refresh();
            return this.paused;
        }
    };

    var extractParent = function (pid) {
        var i = pid.lastIndexOf('/');
        if (i === -1) return User;
        return pid.substr(0, i);
    };

    return {
        ask: ask,
        connect: connect,
        isAsk: isAsk,
        isTell: isTell,
        kill: kill,
        publish: publish,
        reply: reply,
        receive: receive,
        spawn: spawn,
        subscribe: subscribe,
        tell: tell,
        tellDelay: tellDelay,
        tellDeadLetter: deadLetter,
        tellError: error,
        unsubscribe: unsubscribe,
        DeadLetters: DeadLetters,
        Errors: Errors,
        NoSender: NoSender,
        Log: log,
        Root: Root,
        System: System,
        User: User,
        Parent: function () { return context ? extractParent(context.self) : User; },
        Self: function () { return context ? context.self : User; },
        Sender: function () { return context ? context.sender : NoSender; }
    };
})();

window.addEventListener("message", Process.receive, false);

// Knockout.js Process view 
if (typeof ko !== "undefined" && typeof ko.observable !== "undefined") {

    Process.computed = function (fn) {
        return { tag: "Computed", fn: fn };
    };

    Process.clone = function (obj) {
        if (null === obj || "object" !== typeof obj) return obj;
        var copy = obj.constructor();
        for (var attr in obj) {
            if (obj.hasOwnProperty(attr)) copy[attr] = obj[attr];
        }
        return copy;
    };

    Process.cloneMap = function (obj, fn) {
        if (null === obj || "object" !== typeof obj) return obj;
        var copy = obj.constructor();
        for (var attr in obj) {
            if (obj.hasOwnProperty(attr)) copy[attr] = fn(obj[attr]);
        }
        return copy;
    };

    Process.spawnView = function (name, containerId, templateId, setup, inbox, shutdown, options) {

        options = options || {};

        if (typeof setup !== "function") {
            var val = setup;
            setup = function () { return val; };
        }

        return Process.spawn(name,
            // Setup
            function () {

                var view = function (state) {
                    this.render = function (el) {
                        var container = $("#" + containerId)[0];
                        if (container) {
                            ko.cleanNode(container);
                        }
                        ko.applyBindings(state, el)
                    };
                };

                var state = setup();

                var makeObs = function (value) {
                    if (value !== null && typeof value !== "undefined" && typeof value !== "function") {
                        if (value !== null && typeof value.tag === "string" && value.tag === "Computed") {
                            return ko.computed(state[key].fn, state);
                        }
                        if (Object.prototype.toString.call(value) === Object.prototype.toString.call([])) {
                            return ko.observableArray(value);
                        }
                        else {
                            return ko.observable(value);
                        }
                    }
                    else {
                        return value;
                    }
                };

                state.with = function (patch) {
                    var clone = Process.clone(this);
                    for (var key in patch) {
                        if (patch.hasOwnProperty(key)) {
                            var patchValue = patch[key];
                            if (clone.hasOwnProperty(key)) {
                                var clonedValue = clone[key];
                                if (ko.isObservable(clonedValue)) {
                                    clonedValue(patchValue);
                                }
                                else {
                                    clone[key] = makeObs(patchValue);
                                }
                            }
                            else {
                                clone[key] = makeObs(patchValue);
                            }
                        }
                    }
                    return clone;
                };

                if (typeof state === "object") {
                    for (var key in state) {
                        state[key] = makeObs(state[key]);
                    }
                }

                var refresh = function (s) {
                    var el = document.createElement("div");
                    el.innerHTML = $(document.getElementById(templateId)).html();
                    (new view(s)).render(el);
                    $("#" + containerId).empty();
                    $("#" + containerId).append(el);
                };

                refresh(state);

                return {
                    state: state,
                    refresh: refresh
                };
            },
            // Inbox
            function (state, msg) {
                var newState = inbox(state.state, msg);
                if (newState !== state.state) {
                    state.refresh(newState);
                    return {
                        state: newState,
                        refresh: state.refresh
                    };
                }
                else {
                    return state;
                }
            },
            // Shutdown
            function (state) {
                var $container = $("#" + containerId);
                if ($container.length && (!options.preserveDomOnKill)) {
                    ko.cleanNode($container[0]);
                    $container.empty();
                }
                if ("function" === typeof shutdown) {
                    shutdown(state);
                }
            });
    };
}
