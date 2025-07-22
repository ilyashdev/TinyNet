namespace TinyNet.TaskResult;

public class TaskResult<T>
{
   public TaskResult(T result)
   {
      Result = result;
      Status = "OK";
   }

   public TaskResult(string status)
   {
      Status = status;
   }

   public T Result { get; }
   public string Status { get; }
}