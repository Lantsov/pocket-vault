namespace PocketVault.Core;

public sealed class VaultException(string message, Exception? inner = null) : Exception(message, inner);
