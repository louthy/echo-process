namespace Echo.ActorSys2.Configuration
{
    public abstract record Kind
    {
        public static readonly Kind Star = new KnStar();
        public static Kind Arr(Kind x, Kind y) => new KnArr(x, y);

        public abstract string Show();

        public override string ToString() =>
            Show();
    }

    public record KnStar : Kind
    {
        public override string Show() => "*";
    }

    public record KnArr(Kind X, Kind Y) : Kind
    {
        public override string Show() => $"({X.Show()} => {X.Show()})";
    }
}