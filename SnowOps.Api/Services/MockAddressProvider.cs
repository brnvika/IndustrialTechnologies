using System;
using System.Security.Cryptography;

namespace SnowOps.Api.Services;

public interface IMockAddressProvider
{
    string GetRandomMoscowAddress();
}

public class MockAddressProvider : IMockAddressProvider
{
    private readonly string[] _streets =
    {
        "Ленинский проспект", "ул. Тверская", "Кутузовский проспект",
        "ул. Новый Арбат", "Садовое кольцо", "ул. Профсоюзная",
        "проспект Вернадского", "Волгоградский проспект", "ул. Люсиновская",
        "ул. Бауманская", "проспект Мира", "Алтуфьевское шоссе",
        "Варшавское шоссе", "Каширское шоссе", "ул. Вавилова"
    };

    public string GetRandomMoscowAddress()
    {
        var street = _streets[RandomNumberGenerator.GetInt32(_streets.Length)];
        var building = RandomNumberGenerator.GetInt32(1, 150);
        return $"г. Москва, {street}, д. {building}";
    }
}