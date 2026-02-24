using MinorShift.Emuera.Runtime.Script;
using System.Linq;
using System.Reflection;

internal static class RuntimeProcessResolver
{
	private const string RuntimeEngineAssemblyName = "Emuera.RuntimeEngine";
	private const string RuntimeFactoryTypeName = "MinorShift.Emuera.RuntimeEngine.RuntimeProcessFactory";
	private const string RuntimeFactoryMethodName = "Create";

	private static readonly object sync = new();
	private static Func<IExecutionConsole, IRuntimeProcess>? cachedFactory;
	private static bool factoryResolved;

	public static IRuntimeProcess? TryCreate(IExecutionConsole console)
	{
		EnsureFactoryResolved();
		return cachedFactory?.Invoke(console);
	}

	private static void EnsureFactoryResolved()
	{
		if (factoryResolved)
			return;

		lock (sync)
		{
			if (factoryResolved)
				return;

			cachedFactory = ResolveFactory();
			factoryResolved = true;
		}
	}

	private static Func<IExecutionConsole, IRuntimeProcess>? ResolveFactory()
	{
		var envAssembly = Environment.GetEnvironmentVariable("EMUERA_RUNTIME_ENGINE_ASSEMBLY");
		if (!string.IsNullOrWhiteSpace(envAssembly))
		{
			var envFactory = TryResolveFactoryFromPath(envAssembly);
			if (envFactory != null)
				return envFactory;
		}

		var byNameFactory = TryResolveFactoryFromAssemblyName();
		if (byNameFactory != null)
			return byNameFactory;

		var bundledAssemblyPath = Path.Combine(AppContext.BaseDirectory, $"{RuntimeEngineAssemblyName}.dll");
		var bundledFactory = TryResolveFactoryFromPath(bundledAssemblyPath);
		if (bundledFactory != null)
			return bundledFactory;

		return null;
	}

	private static Func<IExecutionConsole, IRuntimeProcess>? TryResolveFactoryFromAssemblyName()
	{
		try
		{
			var loadedAssembly = AppDomain.CurrentDomain
				.GetAssemblies()
				.FirstOrDefault(static asm => string.Equals(asm.GetName().Name, RuntimeEngineAssemblyName, StringComparison.Ordinal));
			if (loadedAssembly != null)
			{
				var loadedFactory = TryResolveFactoryFromAssembly(loadedAssembly);
				if (loadedFactory != null)
					return loadedFactory;
			}

			var assembly = Assembly.Load(new AssemblyName(RuntimeEngineAssemblyName));
			return TryResolveFactoryFromAssembly(assembly);
		}
		catch
		{
			return null;
		}
	}

	private static Func<IExecutionConsole, IRuntimeProcess>? TryResolveFactoryFromPath(string assemblyPath)
	{
		if (string.IsNullOrWhiteSpace(assemblyPath))
			return null;
		if (!File.Exists(assemblyPath))
			return null;

		try
		{
			var assembly = Assembly.LoadFrom(assemblyPath);
			return TryResolveFactoryFromAssembly(assembly);
		}
		catch
		{
			return null;
		}
	}

	private static Func<IExecutionConsole, IRuntimeProcess>? TryResolveFactoryFromAssembly(Assembly assembly)
	{
		var factoryType = assembly.GetType(RuntimeFactoryTypeName, throwOnError: false, ignoreCase: false);
		if (factoryType == null)
			return null;

		var createMethod = factoryType.GetMethod(
			RuntimeFactoryMethodName,
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
			binder: null,
			types: [typeof(IExecutionConsole)],
			modifiers: null);

		if (createMethod == null)
			return null;
		if (!typeof(IRuntimeProcess).IsAssignableFrom(createMethod.ReturnType))
			return null;

		try
		{
			return (Func<IExecutionConsole, IRuntimeProcess>)Delegate.CreateDelegate(
				typeof(Func<IExecutionConsole, IRuntimeProcess>),
				createMethod);
		}
		catch
		{
			return null;
		}
	}
}
