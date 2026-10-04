using System.Net;

namespace TinyNet.Transport;

public interface IConnectionListener : IDisposable                                                                                                                                                                                               
{                                                                                                                                                                                                                                                
    EndPoint EndPoint { get; }                                                                                                                                                                                                                   
    Connection Accept();                                                                                                                                                                                                                         
}                                                                                                                                                                                                                                                
