using Google.OrTools.Sat;

namespace AreYouTheOne.Entities;

public class AytoModel : CpModel
{
    public AytoModel(int couplesCount)
    {
        Vars = new IntVar[couplesCount, couplesCount];
    }
    public IntVar[,] Vars { get; set; }
}