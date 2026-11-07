using Microsoft.AspNetCore.Mvc;

namespace BasslineGenerator.Api.Controllers;

// Abstract controllers and marker interfaces reserve extension points without exposing endpoints.
public interface IAnalysisService;
public interface IVariationService;
public interface IArrangementService;

[ApiController, Route("api/analyze")]
public abstract class AnalyzeController(IAnalysisService service) : ControllerBase
{
    protected IAnalysisService Service { get; } = service;
}

[ApiController, Route("api/variation")]
public abstract class VariationController(IVariationService service) : ControllerBase
{
    protected IVariationService Service { get; } = service;
}

[ApiController, Route("api/arrangement")]
public abstract class ArrangementController(IArrangementService service) : ControllerBase
{
    protected IArrangementService Service { get; } = service;
}
