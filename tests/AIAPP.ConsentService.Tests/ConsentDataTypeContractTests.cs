using System.Reflection;

namespace AIAPP.ConsentService.Tests;

public sealed class ConsentDataTypeContractTests
{
    public static TheoryData<string> DocumentedDataTypes()
    {
        return new TheoryData<string>
        {
            "location_current",
            "location_trail",
            "device_state",
            "chat_ai",
            "location_ai",
            "device_ai",
            "lock_challenge",
            "notifications"
        };
    }

    public static TheoryData<string> UndocumentedDataTypes()
    {
        return new TheoryData<string>
        {
            "chat",
            "location",
            "device",
            "location-current",
            "prompt",
            "wifi_name"
        };
    }

    [Theory]
    [MemberData(nameof(DocumentedDataTypes))]
    public void TryParse_accepts_only_the_documented_data_types(string value)
    {
        var dataTypeType = LoadConsentDataTypeType();
        var parsed = InvokeTryParse(dataTypeType, value);

        Assert.True(parsed.Succeeded);
        Assert.NotNull(parsed.Value);
        Assert.Equal(value, ReadStringProperty(parsed.Value, "Value"));
    }

    [Theory]
    [MemberData(nameof(UndocumentedDataTypes))]
    public void TryParse_rejects_values_outside_the_documented_allow_list(string value)
    {
        var dataTypeType = LoadConsentDataTypeType();
        var parsed = InvokeTryParse(dataTypeType, value);

        Assert.False(parsed.Succeeded);
        Assert.Null(parsed.Value);
    }

    private static Type LoadConsentDataTypeType()
    {
        var assembly = Assembly.Load("AIAPP.ConsentService");
        var type = assembly.GetType("AIAPP.ConsentService.Domain.ConsentDataType");
        Assert.NotNull(type);
        return type;
    }

    private static (bool Succeeded, object? Value) InvokeTryParse(Type dataTypeType, string value)
    {
        var method = dataTypeType.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string), dataTypeType.MakeByRefType()],
            modifiers: null);

        Assert.NotNull(method);
        var arguments = new object?[] { value, null };
        var succeeded = (bool)method.Invoke(null, arguments)!;
        return (succeeded, arguments[1]);
    }

    private static string ReadStringProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return Assert.IsType<string>(property.GetValue(value));
    }
}
