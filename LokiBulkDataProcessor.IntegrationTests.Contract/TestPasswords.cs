using System;
using System.Security.Cryptography;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// A new random container password for each test database (one per fixture), so no password is a constant in the repository.
/// The shape suits both engines: SQL Server's complexity policy (upper and lower case, digits, a symbol, at least 8 characters)
/// and an unquoted Oracle password (a letter first, then letters, digits and <c>_</c>), with nothing that needs escaping in a
/// connection string or a shell.
/// </summary>
public static class TestPasswords
{
    public static string NewPassword() =>
        $"Lk{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}_9X";
}
