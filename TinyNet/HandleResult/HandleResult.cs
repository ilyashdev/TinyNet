namespace TinyNet.TaskResult;

public class HandleResult<T>
{
   public HandleResult(T result)
   {
      Result = result;
      Status = HandleResultStatus.Success;
   }

   public HandleResult(HandleResultStatus status)
   {
      Status = status;
   }

   public T Result { get; }
   public HandleResultStatus Status { get; }
}

public enum HandleResultStatus
{
   Success,
   NotFound,
   Next
}