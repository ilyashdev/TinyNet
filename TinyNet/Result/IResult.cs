using TinyNet.Http;

namespace TinyNet.Result;


public interface IResult
{ 
    void ExecuteResult(ref HttpContext context);
}