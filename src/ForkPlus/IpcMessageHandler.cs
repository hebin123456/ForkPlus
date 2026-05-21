using System.IO.Pipes;

namespace ForkPlus
{
	internal delegate void IpcMessageHandler(NamedPipeServerStream namedPipeClientStream);
}
