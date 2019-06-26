public class Class
{
    public int Add(int a, int b) => a + b;
}

public class System
{
    Class _class;
    int _a, _b;

    public System(Class c, int a, int b)
    {
        _class = c;
        _a = a;
        _b = b;
    }

    public int AddIt() => _class.Add(_a, _b);
    public int Mul() => _a * _b;
}
