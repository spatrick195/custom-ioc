// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// we want to create a IoC. to do this we'll need a resolver and a container.
// the container will store the dependencies, the resolver will resolve them.

TestSingleton();
TestTransient();
TestScoped();

 void TestSingleton()
 {
     Console.WriteLine("Singleton: ");
     var dependencyContainer = new DependencyContainer();
     var singletonResolver = new DependencyResolver(dependencyContainer);
     
     dependencyContainer.AddSingleton<HelloWorld>();
     dependencyContainer.AddSingleton<HelloWorldService>();
     
     var mySingletonService1 = singletonResolver.GetService<HelloWorldService>();
     var mySingletonService2 = singletonResolver.GetService<HelloWorldService>();
     var mySingletonService3 = singletonResolver.GetService<HelloWorldService>();
     
     mySingletonService1.PrintHelloWorld();
     mySingletonService2.PrintHelloWorld();
     mySingletonService3.PrintHelloWorld();
 }
 
 void TestTransient()
 {
     Console.WriteLine("Transient: ");
     
     var dependencyContainer = new DependencyContainer();
     var transientResolver = new DependencyResolver(dependencyContainer);
     
     dependencyContainer.AddTransient<HelloWorld>();
     dependencyContainer.AddTransient<HelloWorldService>();
     
     var myTransientService1 = transientResolver.GetService<HelloWorldService>();
     var myTransientService2 = transientResolver.GetService<HelloWorldService>();
     var myTransientService3 = transientResolver.GetService<HelloWorldService>();
     
     myTransientService1.PrintHelloWorld();
     myTransientService2.PrintHelloWorld();
     myTransientService3.PrintHelloWorld();
 }
 
 void TestScoped()
 {
     Console.WriteLine("Scoped: ");
     var dependencyContainer = new DependencyContainer();
     dependencyContainer.AddScoped<HelloWorld>();
     dependencyContainer.AddScoped<HelloWorldService>();

     using var scope = dependencyContainer.CreateScope();
     var scopedResolver = new DependencyResolver(dependencyContainer, scope);
    
     // Use scopedResolver to resolve your dependencies.
     var myScopedService1 = scopedResolver.GetService<HelloWorldService>();
     var myScopedService2 = scopedResolver.GetService<HelloWorldService>();
     var myScopedService3 = scopedResolver.GetService<HelloWorldService>();
     myScopedService1.PrintHelloWorld();
     myScopedService2.PrintHelloWorld();
     myScopedService3.PrintHelloWorld();
 }


public class Dependency
{
    public Dependency(Type type, DependencyLifetime lifetime)
    {
        Type = type;
        Lifetime = lifetime;
    }
    
    public Type Type { get; set; }
    public DependencyLifetime Lifetime { get; set; }
    
    public object Implementation { get; set; }
    public bool IsImplemented { get; set; }

    public void AddImplementation(object implementation)
    {
        Implementation = implementation;
        IsImplemented = true;
    }
}
public class DependencyResolver
{
    private readonly DependencyContainer _dependencyContainer;

    private readonly DependencyScope _scope;

    public DependencyResolver(DependencyContainer dependencyContainer, DependencyScope scope = null)
    {
        _dependencyContainer = dependencyContainer;
        _scope = scope;
    }

    public object GetService(Type type)
    {
        var dependency = _dependencyContainer.GetDependency(type);
        var constructor = dependency.Type.GetConstructors().Single();
        var parameters = constructor.GetParameters().ToArray();

        if (parameters.Length > 0)
        {
            var parameterImplementation = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                parameterImplementation[i] = GetService(parameters[i].ParameterType);
            return CreateImplementation(dependency, t => Activator.CreateInstance(t, parameterImplementation));
        }

        return CreateImplementation(dependency, t => Activator.CreateInstance(t));
    }

    private object CreateImplementation(Dependency dependency, Func<Type, object> factory)
    {
        if (dependency.IsImplemented)
        {
            return dependency.Implementation;
        }

        if (dependency.Lifetime == DependencyLifetime.Scoped)
        {
            if (_scope == null)
            {
                throw new InvalidOperationException($"Cannot resolve scoped service {dependency.Type} outside of a scope.");
            }

            return _scope.GetScopedDependency(dependency.Type, () => factory(dependency.Type));
        }

        var implementation = factory(dependency.Type);

        if (dependency.Lifetime == DependencyLifetime.Singleton)
        {
            dependency.AddImplementation(implementation);
            return implementation;
        }

        return implementation;
    }

    
    public T GetService<T>()
    {
        return (T) GetService(typeof(T));
    }
}

public class DependencyScope : IDisposable
{
    private readonly Dictionary<Type, object> _scopedObjects = new();

    public object GetScopedDependency(Type type, Func<object> createObject)
    {
        if (!_scopedObjects.TryGetValue(type, out var instance))
        {
            instance = createObject();
            _scopedObjects[type] = instance;
        }

        return instance;
    }

    public void Dispose()
    {
        _scopedObjects.Clear();
    }
}

public class DependencyContainer
{
    private readonly List<Dependency> _dependencies;

    public DependencyContainer()
    {
        _dependencies = new List<Dependency>();
    }

    public void AddSingleton<T>()
    {
        _dependencies.Add(new Dependency(typeof(T), DependencyLifetime.Singleton));
    }

    public void AddTransient<T>()
    {
        _dependencies.Add(new Dependency(typeof(T), DependencyLifetime.Transient));
    }
    
    public void AddScoped<T>()
    {
        _dependencies.Add(new Dependency(typeof(T), DependencyLifetime.Scoped));
    }

    public DependencyScope CreateScope()
    {
        return new DependencyScope();
    }

    public ReadOnlyCollection<Dependency> GetDependencies()
    {
        return _dependencies.AsReadOnly();
    }   

    public Dependency GetDependency(Type type)
    {
        return _dependencies.First(t => t.Type.Name == type.Name);
    }
}

public enum DependencyLifetime : byte
{
    Singleton = 0,
    Transient = 1,
    Scoped = 2
}

public class HelloWorld
{
    private readonly int _random;
    public HelloWorld()
    {
        _random = new Random().Next(1, 100);
    }

    public string GetHelloWorld()
    {
        return $"Hello World {_random}";
    }
}

public class HelloWorldService
{
    private readonly HelloWorld _helloWorld;
    public HelloWorldService(HelloWorld helloWorld)
    {
        _helloWorld = helloWorld;
    }

    public void PrintHelloWorld()
    {
        var helloWorld = _helloWorld.GetHelloWorld();
        Console.WriteLine(helloWorld);
    }
}