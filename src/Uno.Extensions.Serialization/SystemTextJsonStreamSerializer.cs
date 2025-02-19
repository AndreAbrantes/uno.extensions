﻿using System;
using System.IO;
using System.Text.Json;

namespace Uno.Extensions.Serialization;

public class SystemTextJsonStreamSerializer : ISerializer, IStreamSerializer
{
    private readonly JsonSerializerOptions? _serializerOptions;

    public SystemTextJsonStreamSerializer(JsonSerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions;
    }

    public object? ReadFromStream(Stream source, Type targetType)
    {
        return JsonSerializer.Deserialize(source, targetType, _serializerOptions);
    }

    public void WriteToStream(Stream stream, object value, Type valueType)
    {
        JsonSerializer.Serialize(stream, value, valueType, _serializerOptions);
    }

    public string ToString(object value, Type valueType)
    {
        return JsonSerializer.Serialize(value, valueType, _serializerOptions);
    }

    public object? FromString(string source, Type targetType)
    {
        return JsonSerializer.Deserialize(source, targetType, _serializerOptions);
    }
}
