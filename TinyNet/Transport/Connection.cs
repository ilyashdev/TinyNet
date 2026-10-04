using System.Net;

namespace TinyNet.Transport;

  public sealed class Connection : IAsyncDisposable                                                                                                                                                                                                
  {                                                                                                                                                                                                                                                
      public Connection(Stream transport, EndPoint? remoteEndPoint)                                                                                                                                                                                
      {                                                                                                                                                                                                                                            
          Transport = transport;                                                                                                                                                                                                                   
          RemoteEndPoint = remoteEndPoint;                                                                                                                                                                                                         
      }                                                                                                                                                                                                                                            
                                                                                                                                                                                                                                                   
      public Stream Transport { get; }                                                                                                                                                                                                             
      public EndPoint? RemoteEndPoint { get; }                                                                                                                                                                                                     
                                                                                                                                                                                                                                                   
      public ValueTask DisposeAsync() => Transport.DisposeAsync();                                                                                                                                                                                 
  }                