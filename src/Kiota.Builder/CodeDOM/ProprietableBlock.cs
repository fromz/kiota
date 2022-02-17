using System;
using System.Collections.Generic;
using System.Linq;

namespace Kiota.Builder;

public abstract class ProprietableBlock<T, U> : CodeBlock<U, BlockEnd>, IDocumentedElement where T : Enum where U : ProprietableBlockDeclaration, new()
{
    public string Description {get; set;}
    public IEnumerable<CodeProperty> AddProperty(params CodeProperty[] properties)
    {
        if(properties == null || properties.Any(x => x == null))
            throw new ArgumentNullException(nameof(properties));
        if(!properties.Any())
            throw new ArgumentOutOfRangeException(nameof(properties));
        return AddRange(properties);
    }
    public T Kind { get; set; }
    public bool IsOfKind(params T[] kinds) {
        return kinds?.Contains(Kind) ?? false;
    }
    public CodeProperty GetPropertyOfKind(CodePropertyKind kind) =>
    Properties.FirstOrDefault(x => x.IsOfKind(kind));
    public IEnumerable<CodeProperty> Properties => InnerChildElements.Values.OfType<CodeProperty>();
    public IEnumerable<CodeMethod> Methods => InnerChildElements.Values.OfType<CodeMethod>();
    public bool ContainsMember(string name)
    {
        return InnerChildElements.ContainsKey(name);
    }
    public IEnumerable<CodeMethod> AddMethod(params CodeMethod[] methods)
    {
        if(methods == null || methods.Any(x => x == null))
            throw new ArgumentNullException(nameof(methods));
        if(!methods.Any())
            throw new ArgumentOutOfRangeException(nameof(methods));
        return AddRange(methods);
    }
    
}

public class ProprietableBlockDeclaration : BlockDeclaration
{
    private readonly List<CodeType> implements = new ();
    public void AddImplements(params CodeType[] types) {
        if(types == null || types.Any(x => x == null))
            throw new ArgumentNullException(nameof(types));
        EnsureElementsAreChildren(types);
        implements.AddRange(types);
    }
    public IEnumerable<CodeType> Implements => implements;
}


