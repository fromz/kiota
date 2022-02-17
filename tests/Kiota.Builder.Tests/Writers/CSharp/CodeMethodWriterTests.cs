﻿using System;
using System.IO;
using System.Linq;
using Kiota.Builder.Extensions;
using Kiota.Builder.Tests;
using Xunit;

namespace Kiota.Builder.Writers.CSharp.Tests;
public class CodeMethodWriterTests : IDisposable {
    private const string DefaultPath = "./";
    private const string DefaultName = "name";
    private readonly StringWriter tw;
    private readonly LanguageWriter writer;
    private readonly CodeMethod method;
    private readonly CodeClass parentClass;
    private readonly CodeNamespace root;
    private const string MethodName = "methodName";
    private const string ReturnTypeName = "Somecustomtype";
    private const string MethodDescription = "some description";
    private const string ParamDescription = "some parameter description";
    private const string ParamName = "paramName";
    public CodeMethodWriterTests()
    {
        writer = LanguageWriter.GetLanguageWriter(GenerationLanguage.CSharp, DefaultPath, DefaultName);
        tw = new StringWriter();
        writer.SetTextWriter(tw);
        root = CodeNamespace.InitRootNamespace();
        parentClass = new CodeClass {
            Name = "parentClass"
        };
        root.AddClass(parentClass);
        method = new CodeMethod {
            Name = MethodName,
        };
        method.ReturnType = new CodeType {
            Name = ReturnTypeName
        };
        parentClass.AddMethod(method);
    }
    public void Dispose()
    {
        tw?.Dispose();
        GC.SuppressFinalize(this);
    }
    private void AddRequestProperties() {
        parentClass.AddProperty(new CodeProperty {
            Name = "RequestAdapter",
            Kind = CodePropertyKind.RequestAdapter,
        });
        parentClass.AddProperty(new CodeProperty {
            Name = "pathParameters",
            Kind = CodePropertyKind.PathParameters,
        });
        parentClass.AddProperty(new CodeProperty {
            Name = "urlTemplate",
            Kind = CodePropertyKind.UrlTemplate,
        });
    }
    private void AddSerializationProperties() {
        var addData = parentClass.AddProperty(new CodeProperty {
            Name = "additionalData",
            Kind = CodePropertyKind.AdditionalData,
        }).First();
        addData.Type = new CodeType {
            Name = "string"
        };
        var dummyProp = parentClass.AddProperty(new CodeProperty {
            Name = "dummyProp",
        }).First();
        dummyProp.Type = new CodeType {
            Name = "string"
        };
        var dummyCollectionProp = parentClass.AddProperty(new CodeProperty {
            Name = "dummyColl",
        }).First();
        dummyCollectionProp.Type = new CodeType {
            Name = "string",
            CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
        };
        var dummyComplexCollection = parentClass.AddProperty(new CodeProperty {
            Name = "dummyComplexColl"
        }).First();
        dummyComplexCollection.Type = new CodeType {
            Name = "Complex",
            CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            TypeDefinition = new CodeClass {
                Name = "SomeComplexType"
            }
        };
        var dummyEnumProp = parentClass.AddProperty(new CodeProperty{
            Name = "dummyEnumCollection",
        }).First();
        dummyEnumProp.Type = new CodeType {
            Name = "SomeEnum",
            TypeDefinition = new CodeEnum {
                Name = "EnumType"
            }
        };
    }
    private void AddInheritanceClass() {
        (parentClass.StartBlock as ClassDeclaration).Inherits = new CodeType {
            Name = "someParentClass"
        };
    }
    private void AddRequestBodyParameters() {
        var stringType = new CodeType {
            Name = "string",
        };
        method.AddParameter(new CodeParameter {
            Name = "h",
            Kind = CodeParameterKind.Headers,
            Type = stringType,
        });
        method.AddParameter(new CodeParameter{
            Name = "q",
            Kind = CodeParameterKind.QueryParameter,
            Type = stringType,
        });
        method.AddParameter(new CodeParameter{
            Name = "b",
            Kind = CodeParameterKind.RequestBody,
            Type = stringType,
        });
        method.AddParameter(new CodeParameter{
            Name = "r",
            Kind = CodeParameterKind.ResponseHandler,
            Type = stringType,
        });
        method.AddParameter(new CodeParameter {
            Name = "o",
            Kind = CodeParameterKind.Options,
            Type = stringType,
        });
        method.AddParameter(new CodeParameter
        {
            Name = "c",
            Kind = CodeParameterKind.Cancellation,
            Type = stringType,
        });
    }
    [Fact]
    public void WritesRequestBuilder() {
        method.Kind = CodeMethodKind.RequestBuilderBackwardCompatibility;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
    [Fact]
    public void WritesRequestBodiesThrowOnNullHttpMethod() {
        method.Kind = CodeMethodKind.RequestExecutor;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
        method.Kind = CodeMethodKind.RequestGenerator;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
    [Fact]
    public void WritesRequestExecutorBody() {
        method.Kind = CodeMethodKind.RequestExecutor;
        method.HttpMethod = HttpMethod.Get;
        var error4XX = root.AddClass(new CodeClass{
            Name = "Error4XX",
        }).First();
        var error5XX = root.AddClass(new CodeClass{
            Name = "Error5XX",
        }).First();
        var error401 = root.AddClass(new CodeClass{
            Name = "Error401",
        }).First();
        method.ErrorMappings.TryAdd("4XX", new CodeType {Name = "Error4XX", TypeDefinition = error4XX});
        method.ErrorMappings.TryAdd("5XX", new CodeType {Name = "Error5XX", TypeDefinition = error5XX});
        method.ErrorMappings.TryAdd("403", new CodeType {Name = "Error403", TypeDefinition = error401});
        AddRequestBodyParameters();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("var requestInfo", result);
        Assert.Contains("var errorMapping = new Dictionary<string, Func<IParsable>>", result);
        Assert.Contains("{\"4XX\", () => new Error4XX()},", result);
        Assert.Contains("{\"5XX\", () => new Error5XX()},", result);
        Assert.Contains("{\"403\", () => new Error403()},", result);
        Assert.Contains("SendAsync", result);
        Assert.Contains(AsyncKeyword, result);
        Assert.Contains("await", result);
        Assert.Contains("cancellationToken", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void DoesntCreateDictionaryOnEmptyErrorMapping() {
        method.Kind = CodeMethodKind.RequestExecutor;
        method.HttpMethod = HttpMethod.Get;
        AddRequestBodyParameters();
        writer.Write(method);
        var result = tw.ToString();
        Assert.DoesNotContain("var errorMapping = new Dictionary<string, Func<IParsable>>", result);
        Assert.Contains("default", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesRequestExecutorBodyForCollections() {
        method.Kind = CodeMethodKind.RequestExecutor;
        method.HttpMethod = HttpMethod.Get;
        method.ReturnType.CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array;
        AddRequestBodyParameters();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("SendCollectionAsync", result);
        Assert.Contains("cancellationToken", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesRequestGeneratorBody() {
        method.Kind = CodeMethodKind.RequestGenerator;
        method.HttpMethod = HttpMethod.Get;
        AddRequestProperties();
        AddRequestBodyParameters();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("var requestInfo = new RequestInformation", result);
        Assert.Contains("HttpMethod = Method.GET", result);
        Assert.Contains("UrlTemplate = ", result);
        Assert.Contains("PathParameters = ", result);
        Assert.Contains("h?.Invoke", result);
        Assert.Contains("AddQueryParameters", result);
        Assert.Contains("SetContentFromParsable", result);
        Assert.Contains("AddRequestOptions", result);
        Assert.Contains("return requestInfo;", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesInheritedDeSerializerBody() {
        method.Kind = CodeMethodKind.Deserializer;
        AddSerializationProperties();
        AddInheritanceClass();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("base.", result);
        Assert.Contains("new", result);
    }
    [Fact]
    public void WritesDeSerializerBody() {
        method.Kind = CodeMethodKind.Deserializer;
        AddSerializationProperties();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("GetStringValue", result);
        Assert.Contains("GetCollectionOfPrimitiveValues", result);
        Assert.Contains("GetCollectionOfObjectValues", result);
        Assert.Contains("GetEnumValue", result);
    }
    [Fact]
    public void WritesInheritedSerializerBody() {
        method.Kind = CodeMethodKind.Serializer;
        method.IsAsync = false;
        AddSerializationProperties();
        AddInheritanceClass();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("base.Serialize(writer);", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesSerializerBody() {
        var parameter = new CodeParameter{
            Description = ParamDescription,
            Name = ParamName
        };
        parameter.Type = new CodeType {
            Name = "string"
        };
        method.Kind = CodeMethodKind.Serializer;
        method.IsAsync = false;
        AddSerializationProperties();
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("WriteStringValue", result);
        Assert.Contains("WriteCollectionOfPrimitiveValues", result);
        Assert.Contains("WriteCollectionOfObjectValues", result);
        Assert.Contains("WriteEnumValue", result);
        Assert.Contains("WriteAdditionalData(additionalData);", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesMethodAsyncDescription() {
        
        method.Description = MethodDescription;
        var parameter = new CodeParameter{
            Description = ParamDescription,
            Name = ParamName
        };
        parameter.Type = new CodeType {
            Name = "string"
        };
        method.AddParameter(parameter);
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("/// <summary>", result);
        Assert.Contains(MethodDescription, result);
        Assert.Contains("<param name=", result);
        Assert.Contains("</param>", result);
        Assert.Contains(ParamName, result);
        Assert.Contains(ParamDescription, result); 
        Assert.Contains("</summary>", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesMethodSyncDescription() {
        
        method.Description = MethodDescription;
        method.IsAsync = false;
        var parameter = new CodeParameter{
            Description = ParamDescription,
            Name = ParamName
        };
        parameter.Type = new CodeType {
            Name = "string"
        };
        method.AddParameter(parameter);
        writer.Write(method);
        var result = tw.ToString();
        Assert.DoesNotContain("@returns a Promise of", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void Defensive() {
        var codeMethodWriter = new CodeMethodWriter(new CSharpConventionService());
        Assert.Throws<ArgumentNullException>(() => codeMethodWriter.WriteCodeElement(null, writer));
        Assert.Throws<ArgumentNullException>(() => codeMethodWriter.WriteCodeElement(method, null));
        var originalParent = method.Parent;
        method.Parent = CodeNamespace.InitRootNamespace();
        Assert.Throws<InvalidOperationException>(() => codeMethodWriter.WriteCodeElement(method, writer));
        method.Parent = originalParent;
        method.ReturnType = null;
        Assert.Throws<InvalidOperationException>(() => codeMethodWriter.WriteCodeElement(method, writer));
    }
    [Fact]
    public void ThrowsIfParentIsNotClass() {
        method.Parent = CodeNamespace.InitRootNamespace();
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
    [Fact]
    public void ThrowsIfReturnTypeIsMissing() {
        method.ReturnType = null;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
    private const string TaskPrefix = "Task<";
    private const string AsyncKeyword = "async";
    [Fact]
    public void WritesReturnType() {
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains($"{AsyncKeyword} {TaskPrefix}{ReturnTypeName}> {MethodName.ToFirstCharacterUpperCase()}", result); // async default
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void DoesNotAddUndefinedOnNonNullableReturnType(){
        method.ReturnType.IsNullable = false;
        writer.Write(method);
        var result = tw.ToString();
        Assert.DoesNotContain("?", result);
    }
    [Fact]
    public void DoesNotAddAsyncInformationOnSyncMethods() {
        method.IsAsync = false;
        writer.Write(method);
        var result = tw.ToString();
        Assert.DoesNotContain(TaskPrefix, result);
        Assert.DoesNotContain(AsyncKeyword, result);
    }
    [Fact]
    public void WritesPublicMethodByDefault() {
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("public ", result);// public default
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesPrivateMethod() {
        method.Access = AccessModifier.Private;
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("private ", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesProtectedMethod() {
        method.Access = AccessModifier.Protected;
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("protected ", result);
        AssertExtensions.CurlyBracesAreClosed(result);
    }
    [Fact]
    public void WritesPathParameterRequestBuilder() {
        AddRequestProperties();
        method.Kind = CodeMethodKind.RequestBuilderWithParameters;
        method.AddParameter(new CodeParameter {
            Name = "pathParam",
            Kind = CodeParameterKind.Path,
            Type = new CodeType {
                Name = "string"
            }
        });
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains("RequestAdapter", result);
        Assert.Contains("PathParameters", result);
        Assert.Contains("pathParam", result);
        Assert.Contains("return new", result);
    }
    [Fact]
    public void WritesConstructor() {
        method.Kind = CodeMethodKind.Constructor;
        var defaultValue = "someVal";
        var propName = "propWithDefaultValue";
        parentClass.AddProperty(new CodeProperty {
            Name = propName,
            DefaultValue = defaultValue,
            Kind = CodePropertyKind.UrlTemplate,
        });
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains(parentClass.Name.ToFirstCharacterUpperCase(), result);
        Assert.Contains($"{propName.ToFirstCharacterUpperCase()} = {defaultValue}", result);
    }
    [Fact]
    public void WritesApiConstructor() {
        method.Kind = CodeMethodKind.ClientConstructor;
        var coreProp = parentClass.AddProperty(new CodeProperty {
            Name = "core",
            Kind = CodePropertyKind.RequestAdapter,
        }).First();
        coreProp.Type = new CodeType {
            Name = "RequestAdapter",
            IsExternal = true,
        };
        method.AddParameter(new CodeParameter {
            Name = "core",
            Kind = CodeParameterKind.RequestAdapter,
            Type = coreProp.Type,
        });
        method.DeserializerModules = new() {"com.microsoft.kiota.serialization.Deserializer"};
        method.SerializerModules = new() {"com.microsoft.kiota.serialization.Serializer"};
        writer.Write(method);
        var result = tw.ToString();
        Assert.Contains(parentClass.Name.ToFirstCharacterUpperCase(), result);
        Assert.Contains("RegisterDefaultSerializer", result);
        Assert.Contains("RegisterDefaultDeserializer", result);
    }
    [Fact]
    public void WritesApiConstructorWithBackingStore() {
        method.Kind = CodeMethodKind.ClientConstructor;
        var coreProp = parentClass.AddProperty(new CodeProperty {
            Name = "core",
            Kind = CodePropertyKind.RequestAdapter,
        }).First();
        coreProp.Type = new CodeType {
            Name = "RequestAdapter",
            IsExternal = true,
        };
        method.AddParameter(new CodeParameter {
            Name = "core",
            Kind = CodeParameterKind.RequestAdapter,
            Type = coreProp.Type,
        });
        var backingStoreParam = new CodeParameter {
            Name = "backingStore",
            Kind = CodeParameterKind.BackingStore,
        };
        backingStoreParam.Type = new CodeType {
            Name = "IBackingStore",
            IsExternal = true,
        };
        method.AddParameter(backingStoreParam);
        var tempWriter = LanguageWriter.GetLanguageWriter(GenerationLanguage.CSharp, DefaultPath, DefaultName);
        tempWriter.SetTextWriter(tw);
        tempWriter.Write(method);
        var result = tw.ToString();
        Assert.Contains("EnableBackingStore", result);
    }
    [Fact]
    public void ThrowsOnGetter() {
        method.Kind = CodeMethodKind.Getter;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
    [Fact]
    public void ThrowsOnSetter() {
        method.Kind = CodeMethodKind.Setter;
        Assert.Throws<InvalidOperationException>(() => writer.Write(method));
    }
}    
