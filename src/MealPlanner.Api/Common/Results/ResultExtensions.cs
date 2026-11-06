namespace MealPlanner.Api.Common.Results;

public static class ResultExtensions
{
    /// <summary>200 with the value, or a problem response matching the error.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) => result.IsSuccess
        ? TypedResults.Ok(result.Value)
        : TypedResults.Problem(result.Error!.Message, statusCode: result.Error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        });
}
