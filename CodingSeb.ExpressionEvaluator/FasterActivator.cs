/* This high performance alternative to Activator uses System.Reflection.Emit namespace 
 * to generate methods that can dynamically create objects at runtime. Used properly, 
 * this single .cs file can help if you have performance bottlenecks with Activator. 
 * Part of Code Blocks (http://codeblocks.codeplex.com)
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

static class FastActivator
{
    public static IDictionary<string, DynamicCreationDelegate> Cache { get; set; } = new Dictionary<string, DynamicCreationDelegate>(); 
    public delegate object DynamicCreationDelegate(object[] arguments);

    private static DynamicMethod MakeCreationMethodBoxed(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
    {
        var constructor = returnType.GetConstructor(paramTypes);

        if (constructor == null)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                              "Could not find constructor matching signature {0}({1})", returnType.FullName, string.Join(",", from argument in paramTypes
                                                                                                                                                              select argument.FullName)));
        var constructorParams = constructor.GetParameters();
        var method =
            new DynamicMethod(
                string.Format("{0}__{1}", constructor.DeclaringType.Name, Guid.NewGuid().ToString().Replace("-", "")), typeof(object),
                new[] { typeof(object[]) }, restrictedSkipVisibility);

        ILGenerator gen = method.GetILGenerator();
        gen.Emit(OpCodes.Nop);
        for (int i = 0; i < constructorParams.Length; i++)
        {
            Type paramType = constructorParams[i].ParameterType;

            gen.Emit(OpCodes.Ldarg_0);
            switch (i)
            {
                case 0:
                    gen.Emit(OpCodes.Ldc_I4_0);
                    break;

                case 1:
                    gen.Emit(OpCodes.Ldc_I4_1);
                    break;

                case 2:
                    gen.Emit(OpCodes.Ldc_I4_2);
                    break;

                case 3:
                    gen.Emit(OpCodes.Ldc_I4_3);
                    break;

                case 4:
                    gen.Emit(OpCodes.Ldc_I4_4);
                    break;

                case 5:
                    gen.Emit(OpCodes.Ldc_I4_5);
                    break;

                case 6:
                    gen.Emit(OpCodes.Ldc_I4_6);
                    break;

                case 7:
                    gen.Emit(OpCodes.Ldc_I4_7);
                    break;

                case 8:
                    gen.Emit(OpCodes.Ldc_I4_8);
                    break;

                default:
                    gen.Emit(OpCodes.Ldc_I4_S, i);
                    break;
            }

            gen.Emit(OpCodes.Ldelem_Ref);
            gen.Emit(paramType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, paramType);
        }

        gen.Emit(OpCodes.Newobj, constructor);
        gen.Emit(OpCodes.Ret);

        return method;
    }
    private static DynamicMethod MakeCreationMethodTypeSafe(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
    {
        var constructor = returnType.GetConstructor(paramTypes);

        if (constructor == null)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                              "Could not find constructor matching signature {0}({1})", returnType.FullName, string.Join(",", from argument in paramTypes
                                                                                                                                                              select argument.FullName)));
        var constructorParams = constructor.GetParameters();

        var method =
            new DynamicMethod(
                string.Format("{0}__{1}", constructor.DeclaringType.Name, Guid.NewGuid().ToString().Replace("-", "")), constructor.DeclaringType,
                (from param in constructorParams select param.ParameterType).ToArray(), restrictedSkipVisibility);

        ILGenerator gen = method.GetILGenerator();
        for (int i = 0; i < constructorParams.Length; i++)
            if (i < 4)
                switch (i)
                {
                    case 0:
                        gen.Emit(OpCodes.Ldarg_0);
                        break;
                    case 1:
                        gen.Emit(OpCodes.Ldarg_1);
                        break;
                    case 2:
                        gen.Emit(OpCodes.Ldarg_2);
                        break;
                    case 3:
                        gen.Emit(OpCodes.Ldarg_3);
                        break;
                }
            else
                gen.Emit(OpCodes.Ldarg_S, i);  // Only up to 255 args

        gen.Emit(OpCodes.Newobj, constructor);
        gen.Emit(OpCodes.Ret);

        return method;
    }

    public static DynamicCreationDelegate GenerateDelegate(Type returnType, params Type[] paramTypes)
    {
        string key = returnType.FullName + "|" + string.Join("|", paramTypes.Select(t => t.FullName));
        if (!Cache.ContainsKey(key))
            Cache[key] = GenerateDelegate(false, returnType, paramTypes);

        return Cache[key];
    }
    public static DynamicCreationDelegate GenerateDelegate(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
    {
        var constructor = returnType.GetConstructor(paramTypes);
        if (constructor == null)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                              "Could not find constructor matching signature {0}({1})",
                                                              returnType.FullName,
                                                              string.Join(",", from param in paramTypes
                                                                               select param.FullName)));

        var creator = MakeCreationMethodBoxed(restrictedSkipVisibility, returnType, paramTypes);
        return (DynamicCreationDelegate)creator.CreateDelegate(typeof(DynamicCreationDelegate));
    }

    public static T GenerateFunc<T>()
        where T : class
    {
        return GenerateFunc<T>(false);
    }
    public static T GenerateFunc<T>(bool restrictedSkipVisibility)
        where T : class
    {
        var delegateType = typeof(T);
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(string.Format("{0} is not a delegate type", delegateType.FullName));

        // Validate the delegate return type
        MethodInfo delMethod = delegateType.GetMethod("Invoke");
        var parameters = delMethod.GetParameters();
        var returnType = delMethod.ReturnType;
        if (returnType == typeof(void))
            throw new InvalidOperationException("Cannot register a delegate that doesn't return anything!");

        var paramTypes = (from parameter in parameters select parameter.ParameterType).ToArray();
        var constructor = returnType.GetConstructor(paramTypes);
        if (constructor == null)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                              "Could not find constructor matching signature {0}({1})",
                                                              returnType.FullName,
                                                              string.Join(",", from param in paramTypes
                                                                               select param.FullName)));

        if (delMethod.ReturnType != constructor.DeclaringType)
            throw new InvalidOperationException("The return type of the delegate must match the constructors declaring type");

        // Validate the signatures
        ParameterInfo[] delParams = delMethod.GetParameters();
        ParameterInfo[] constructorParam = constructor.GetParameters();
        if (delParams.Length != constructorParam.Length)
            throw new InvalidOperationException("The delegate signature does not match that of the constructor");

        var method = MakeCreationMethodTypeSafe(restrictedSkipVisibility, returnType, paramTypes);
        return method.CreateDelegate(delegateType) as T;
    }
}