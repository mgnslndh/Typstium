using Typstium.Capabilities;
using Typstium.Workspace;

namespace Typstium.CommandLine;

internal sealed record CompileArgumentsInput(TypstCompileRequest Request, WorkspaceLayout Layout, TypstCapabilities Capabilities);
