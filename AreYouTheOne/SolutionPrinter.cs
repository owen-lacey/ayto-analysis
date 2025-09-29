using System.Diagnostics;
using AreYouTheOne.Entities;
using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;

namespace AreYouTheOne;

public class SolutionPrinter : CpSolverSolutionCallback
{
    public List<List<int>> AllSolutions { get; set; } = [];
    private readonly AytoModel _model;
    private readonly Stopwatch _timer = new();

    public SolutionPrinter(AytoModel model, ILogger logger)
    {
        _model = model;
        _logger = logger;
        _timer.Start();
    }
    
    public override void OnSolutionCallback()
    {
        var length = _model.Vars.GetLength(0);
        var solution = new int[length];
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < length; j++)
            {
                if (Value(_model.Vars[i, j]) == 1)
                {
                    solution[i] = j;
                    break;
                }
            }
        }

        AllSolutions.Add(solution.ToList());

        if (++_count % 10000 == 0)
        {
            _logger.LogDebug($"Solutions found: {_count}. Time: {DateTime.Now:HH:mm:ss}. Elapsed: {_timer.Elapsed:g}");
        }
    }

    public int SolutionCount()
    {
        return _count;
    }

    private int _count;
    private ILogger _logger;
}