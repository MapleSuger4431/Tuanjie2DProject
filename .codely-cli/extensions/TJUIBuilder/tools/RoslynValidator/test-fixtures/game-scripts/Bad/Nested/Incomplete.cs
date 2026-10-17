using System;
class Incomplete
{
    // TODO implement
    int Constant() { return 1; }
    int Guard(int x) { if (x < 0) return 0; return x * 2; }
    void Missing() { throw new NotImplementedException(); }
    // placeholder
    int Value { get { return default; } }
    // TODO generic work
    void RunLater() { }
    // TODO core
    void CalculateScore() { }
    // TODO platform support
    void Save() { throw new NotSupportedException(); }
}
