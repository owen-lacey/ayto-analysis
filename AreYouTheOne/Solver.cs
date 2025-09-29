using System.Reflection;
using System.Text.Json;
using AreYouTheOne.Entities;
using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;

namespace AreYouTheOne;

public class Solver(SeasonData input, ILogger logger, List<Event> events)
{
    public List<Event> Events { get; } = events;
    private readonly AytoModel _model = GetModel(input);
    private readonly ILogger _logger = logger;

    // Assume equal numbers of men/women

    public List<BoundedLinearExpression> BuildConstraints(SeasonData seasonData)
    {
        List<BoundedLinearExpression> constraints = [];
        foreach (var @event in seasonData.Events)
        {
            foreach (var truthBooth in @event.TruthBooths)
            {
                var boundedLinearExpression = _model.Vars[truthBooth.A, truthBooth.B] == (truthBooth.Result ? 1 : 0);
                constraints.Add(boundedLinearExpression);
            }

            List<IntVar> selections = [];
            if (@event.MatchUp != null)
            {
                foreach (var pair in @event.MatchUp.Matching)
                {
                    selections.Add(_model.Vars[pair.A, pair.B]);
                }

                constraints.Add(LinearExpr.Sum(selections) == @event.MatchUp.Score);
            }
        }

        return constraints;
    }

    private List<AytoResult> SolveEachConstraint(List<BoundedLinearExpression> constraints)
    {
        List<AytoResult> possibleMatchCounts = [];
        var i = 0;
        foreach (var constraint in constraints)
        {
            i++;
            _model.Add(constraint);
            if (i >= 0)
            {
                possibleMatchCounts.Add(CalculateRemainingMatches());
                _logger.LogDebug($"Counts so far: {string.Join(", ", possibleMatchCounts)}");
            }
        }

        return possibleMatchCounts;
    }

    public AytoResult SolveAllConstraints(List<BoundedLinearExpression> constraints)
    {
        foreach (var constraint in constraints)
        {
            _model.Add(constraint);
        }

        return CalculateRemainingMatches();
    }

    private AytoResult CalculateRemainingMatches()
    {
        var solver = new CpSolver();
        var cb = new SolutionPrinter(_model, _logger);
        solver.StringParameters = "enumerate_all_solutions:true";
        var status = solver.Solve(_model, cb);
        if (status == CpSolverStatus.Infeasible)
        {
            throw new Exception("No solution found");
        }

        return new AytoResult(Events)
        {
            RemainingMatches = cb.AllSolutions.Select(solution => solution.ToList()).ToList()
        };
    }

    private static AytoModel GetModel(SeasonData seasonData)
    {
        // Assume equal numbers of men/women
        var pairs = seasonData.Contestants.Men.Length;
        var model = new AytoModel(pairs);
        for (int i = 0; i < pairs; ++i)
        {
            for (int j = 0; j < pairs; j++)
            {
                model.Vars[i, j] = model.NewIntVar(0, 1, $"{seasonData.Contestants.Men[i].Name.Substring(0, 2)}{i}_{seasonData.Contestants.Women[j].Name.Substring(0, 2)}{j}");
            }
        }
        // Constraint: Each row sums to 1
        for (int i = 0; i < pairs; i++)
        {
            var row = new IntVar[pairs];
            for (int j = 0; j < pairs; j++)
            {
                row[j] = model.Vars[i, j];
            }
            model.Add(LinearExpr.Sum(row) == 1);
        }

        // Constraint: Each column sums to 1
        for (int j = 0; j < pairs; j++)
        {
            var column = new IntVar[pairs];
            for (int i = 0; i < pairs; i++)
            {
                column[i] = model.Vars[i, j];
            }
            model.Add(LinearExpr.Sum(column) == 1);
        }

        
        return model;
    }
}