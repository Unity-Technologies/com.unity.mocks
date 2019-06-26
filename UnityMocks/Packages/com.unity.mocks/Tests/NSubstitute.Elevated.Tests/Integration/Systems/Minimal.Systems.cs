public class Inner
{
    public Inner() { }

    public int Add(int a, int b) => a + b;
}

public class Outer
{
    Inner _c;
    int _a, _b;

    public Outer(Inner c, int a, int b)
    {
        _c = c;
        _a = a;
        _b = b;
    }

    public int AddIt() => _c.Add(_a, _b);
    public int Mul() => _a * _b;
}
