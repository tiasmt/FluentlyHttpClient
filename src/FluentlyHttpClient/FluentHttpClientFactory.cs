namespace FluentlyHttpClient;

public record FluentHttpClientFactoryOptions(
	Action<FluentHttpClientBuilder>? ConfigureDefaults
);
/// <summary>
/// HTTP client factory which contains registered HTTP clients and able to get existing or creating new ones.
/// </summary>
public interface IFluentHttpClientFactory
{
	/// <summary>
	/// Creates a new <see cref="FluentHttpClientBuilder"/>.
	/// </summary>
	/// <param name="identifier">identifier to set.</param>
	/// <returns>Returns a new HTTP client builder.</returns>
	FluentHttpClientBuilder CreateBuilder(string identifier);

	/// <summary>
	/// Configure defaults for <see cref="FluentHttpClientBuilder"/> which every new one uses.
	/// </summary>
	/// <param name="configure">Configuration function.</param>
	/// <returns>Returns client factory for chaining.</returns>
	IFluentHttpClientFactory ConfigureDefaults(Action<FluentHttpClientBuilder> configure);

	/// <summary>
	/// Get <see cref="IFluentHttpClient"/> registered by identifier.
	/// </summary>
	/// <param name="identifier">Identifier to get.</param>
	/// <exception cref="KeyNotFoundException">Throws an exception when key is not found.</exception>
	/// <returns>Returns HTTP client.</returns>
	IFluentHttpClient Get(string identifier);

	/// <summary>
	/// Add/register HTTP client.
	/// </summary>
	/// <param name="client">Client to register.</param>
	/// <returns>Returns HTTP client.</returns>
	IFluentHttpClient Add(IFluentHttpClient client);

	/// <summary>
	/// Add/register HTTP client from builder.
	/// </summary>
	/// <param name="clientBuilder">Client builder to register.</param>
	/// <returns>Returns HTTP client created.</returns>
	IFluentHttpClient Add(FluentHttpClientBuilder clientBuilder);

	/// <summary>
	/// Remove/unregister HTTP client.
	/// </summary>
	/// <param name="identifier">Identity to remove.</param>
	/// <returns>Returns client factory for chaining.</returns>
	IFluentHttpClientFactory Remove(string identifier);

	/// <summary>
	/// Determine whether identifier is already registered.
	/// </summary>
	/// <param name="identifier">Identifier to check.</param>
	/// <returns>Returns true when already exists.</returns>
	bool Has(string identifier);

	/// <summary>
	/// Get the amount of clients registered.
	/// </summary>
	int Count { get; }
}

/// <summary>
/// Class which contains registered <see cref="IFluentHttpClient"/> and able to get existing or creating new ones.
/// </summary>
public class FluentHttpClientFactory : IFluentHttpClientFactory
{
	private readonly IServiceProvider _serviceProvider;
	private readonly Dictionary<string, IFluentHttpClient> _clientsMap = [];
	private Action<FluentHttpClientBuilder>? _configure;

	public int Count => _clientsMap.Count;

	/// <summary>
	/// Initializes a new instance of <see cref="FluentHttpClientFactory"/>.
	/// </summary>
	/// <param name="serviceProvider"></param>
	/// <param name="options"></param>
	public FluentHttpClientFactory(
		IServiceProvider serviceProvider,
		FluentHttpClientFactoryOptions options
	)
	{
		_serviceProvider = serviceProvider;
		if (options.ConfigureDefaults != null)
			ConfigureDefaults(options.ConfigureDefaults);
	}

	/// <inheritdoc />
	public FluentHttpClientBuilder CreateBuilder(string identifier)
	{
		var clientBuilder = ActivatorUtilities.CreateInstance<FluentHttpClientBuilder>(_serviceProvider, this)
			.WithIdentifier(identifier)
			.WithUserAgent("fluently")
			.WithTimeout(15);

		_configure?.Invoke(clientBuilder);

		return clientBuilder;
	}

	/// <inheritdoc />
	public IFluentHttpClientFactory ConfigureDefaults(Action<FluentHttpClientBuilder> configure)
	{
		_configure = configure;
		return this;
	}

	/// <inheritdoc />
	public IFluentHttpClient Get(string identifier)
	{
		if (!_clientsMap.TryGetValue(identifier, out var client))
			throw new KeyNotFoundException($"FluentHttpClient '{identifier}' not registered.");
		return client;
	}

	/// <inheritdoc />
	public IFluentHttpClient Add(IFluentHttpClient client)
	{
		ArgumentNullException.ThrowIfNull(client);

		if (Has(client.Identifier))
			throw new ClientBuilderValidationException($"FluentHttpClient '{client.Identifier}' is already registered.");
		_clientsMap.Add(client.Identifier, client);
		return client;
	}

	/// <inheritdoc />
	public IFluentHttpClient Add(FluentHttpClientBuilder clientBuilder)
	{
		ArgumentNullException.ThrowIfNull(clientBuilder);

		var client = clientBuilder.Build(skipAutoRegister: true);
		return Add(client);
	}

	/// <inheritdoc />
	public IFluentHttpClientFactory Remove(string identifier)
	{
		if (!_clientsMap.Remove(identifier, out var client))
			return this;

		client.Dispose();
		return this;
	}

	/// <inheritdoc />
	public bool Has(string identifier) => _clientsMap.ContainsKey(identifier);
}