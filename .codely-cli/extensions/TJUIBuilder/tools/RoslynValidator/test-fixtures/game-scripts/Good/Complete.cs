interface IGood { void Run(); }
abstract class Base { public abstract int Read(); }
class Complete
{
    int Guard(int x) { if (x < 0) return 0; return x * 2; }
    const int LegalConstant = 3;
    void Update() { }
}
