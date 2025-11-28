using Microsoft.VisualStudio.TestTools.UnitTesting;

// Explicitly enable test parallelization for better performance
// This resolves MSTEST0001 warning
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
