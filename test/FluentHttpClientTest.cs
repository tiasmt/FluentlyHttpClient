using Microsoft.Extensions.DependencyInjection;
using Sketch7.MessagePack.MediaTypeFormatter;
using static FluentlyHttpClient.Test.ServiceTestUtil;

// ReSharper disable once CheckNamespace
namespace Test;

public class HttpClient
{
	private readonly MessagePackMediaTypeFormatter _messagePackMediaTypeFormatter = new();

	[Fact]
	public async void Get_ShouldReturnContent()
	{
		var mockHttp = new MockHttpMessageHandler();
		mockHttp.When("https://sketch7.com/api/heroes/azmodan")
			.Respond("application/json", "{ 'name': 'Azmodan' }");

		var httpClient = GetNewClientFactory().CreateBuilder("sketch7")
			.WithBaseUrl("https://sketch7.com")
			.WithMockMessageHandler(mockHttp)
			.Build();

		var hero = await httpClient.Get<Hero>("/api/heroes/azmodan");

		Assert.NotNull(hero);
		Assert.Equal("Azmodan", hero.Name);
	}

	[Fact]
	public async void Post_ShouldReturnContent()
	{
		var mockHttp = new MockHttpMessageHandler();
		mockHttp.When(HttpMethod.Post, "https://sketch7.com/api/heroes/azmodan")
			//.WithContent("{\"Title\":\"Lord of Sin\"}")
			.With(request =>
			{
				var contentTask = request.Content.ReadAsAsync<Hero>();
				contentTask.Wait();
				return contentTask.Result.Title == "Lord of Sin";
			})
			.Respond("application/json", "{ 'name': 'Azmodan', 'title': 'Lord of Sin' }");

		var httpClient = GetNewClientFactory().CreateBuilder("sketch7")
			.WithBaseUrl("https://sketch7.com")
			.WithMockMessageHandler(mockHttp)
			.Build();

		var hero = await httpClient.Post<Hero>("/api/heroes/azmodan", new
		{
			Title = "Lord of Sin"
		});

		Assert.NotNull(hero);
		Assert.Equal("Lord of Sin", hero.Title);
	}

	[Fact]
	public void CreateClient_ShouldInheritOptions()
	{
		var serviceProvider = CreateContainer().BuildServiceProvider();
		var httpClientFactory = serviceProvider.GetRequiredService<IFluentHttpClientFactory>();
		var clientBuilder = httpClientFactory.CreateBuilder("sketch7")
				.WithBaseUrl("https://sketch7.com")
				.WithHeader("locale", "en-GB")
				.UseTimer()
				.ConfigureFormatters(x => x.Formatters.Add(_messagePackMediaTypeFormatter))
				.WithRequestBuilderDefaults(requestBuilder =>
				{
					requestBuilder.WithMethod(HttpMethod.Trace)
						.WithUri("api/graphql")
						.WithItem("error-mapping", "map this")
						.WithItem("context", "user")
						;
				})
			;

		var httpClient = clientBuilder.Build();
		var subClient = httpClient.CreateClient("subclient")
			.WithRequestBuilderDefaults(x => x.WithItem("context", "reward"))
			.WithHeader("locale", "de")
			.WithHeader("country", "de")
			.UseLogging()
			.Build();

		var httpClientRequest = httpClient.CreateRequest();
		var subClientRequest = subClient.CreateRequest();

		var httpClientLocale = httpClient.Headers.GetValues("locale").FirstOrDefault();
		var subClientLocale = subClient.Headers.GetValues("locale").FirstOrDefault();

		httpClient.Headers.TryGetValues("country", out var countryValues);
		var subClientCountry = subClient.Headers.GetValues("country").FirstOrDefault();

		Assert.Equal("sketch7", httpClient.Identifier);
		Assert.Equal("sketch7.subclient", subClient.Identifier);
		Assert.Equal("en-GB", httpClientLocale);
		Assert.Equal("de", subClientLocale);
		Assert.Null(countryValues?.FirstOrDefault());
		Assert.Equal("de", subClientCountry);

		Assert.Equal(httpClientRequest.HttpMethod, subClientRequest.HttpMethod);
		Assert.Equal(httpClientRequest.Items["error-mapping"], subClientRequest.Items["error-mapping"]);
		Assert.Equal("user", httpClientRequest.Items["context"]);
		Assert.Equal("reward", subClientRequest.Items["context"]);
		Assert.Equal(httpClient.Formatters.Count, subClient.Formatters.Count);
		// todo: check middleware count?

		Assert.Equal(2, httpClientFactory.Count);
	}

	[Fact]
	public async void GraphQL_ShouldReturnContent()
	{
		const string query = "{hero {name,title}}";
		const string operationName = "heroGet";

		var mockHttp = new MockHttpMessageHandler();
		mockHttp.When(HttpMethod.Post, "https://sketch7.com/api/graphql")
			.With(request =>
			{
				var contentTask = request.Content.ReadAsAsync<GqlRequest>();
				contentTask.Wait();
				var result = contentTask.Result;
				return result.Query == query
					   && result.OperationName == operationName;
			})
			.Respond("application/json", "{ 'data': {'name': 'Azmodan', 'title': 'Lord of Sin' }}");

		var httpClient = GetNewClientFactory().CreateBuilder("sketch7")
			.WithBaseUrl("https://sketch7.com")
			.WithRequestBuilderDefaults(requestBuilder => requestBuilder.WithUri("api/graphql"))
			.WithMockMessageHandler(mockHttp)
			.Build();

		var response = await httpClient.CreateGqlRequest(query, operationName)
			.ReturnAsGqlResponse<Hero>();

		Assert.True(response.IsSuccessStatusCode);
		Assert.NotNull(response.Data);
		Assert.Equal("Lord of Sin", response.Data.Title);
	}
}