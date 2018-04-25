// MIT License
//   
// Copyright(c) 2017 graphql-dotnet
// Copyright(c) 2018 Microsoft
//   
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace Xamarin.Forms.GraphQL.Client
{
	public class GraphQLClient : IDisposable
	{
		readonly HttpClient httpClient;

		public HttpRequestHeaders DefaultRequestHeaders => httpClient.DefaultRequestHeaders;
		public GraphQLClientOptions Options { get; }

		public GraphQLClient(Uri endPoint) : this(new GraphQLClientOptions (endPoint))
		{
		}

		public GraphQLClient(GraphQLClientOptions options)
		{
			Options = options ?? throw new ArgumentNullException(nameof(options));
			httpClient = new HttpClient(Options.HttpMessageHandler);
		}

		public Task<GraphQLResponse> GetQueryAsync(string query, string variables = null, CancellationToken cancellationToken = default(CancellationToken))
			=> GetAsync(new GraphQLRequest { Query = query ?? throw new ArgumentNullException(nameof(query)), Variables = variables }, cancellationToken);

		public async Task<GraphQLResponse> GetAsync(GraphQLRequest request, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			if (request.Query == null)
				throw new ArgumentNullException(nameof(request.Query));

			var queryParams = request.Variables == null ? request.Query : $"{request.Query}&variables={JsonConvert.SerializeObject(request.Variables)}";
			using (var httpResponseMessage = await httpClient.GetAsync($"{Options.EndPoint}?{queryParams}", cancellationToken).ConfigureAwait(false)) {
				return await ReadHttpResponseMessageAsync(httpResponseMessage).ConfigureAwait(false);
			}
		}

		public Task<GraphQLResponse> PostQueryAsync(string query, string variables = null, CancellationToken cancellationToken = default(CancellationToken))
			=> PostAsync(new GraphQLRequest { Query = query ?? throw new ArgumentNullException(nameof(query)), Variables = variables }, cancellationToken);

		public async Task<GraphQLResponse> PostAsync(GraphQLRequest request, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			if (request.Query == null)
				throw new ArgumentNullException(nameof(request.Query));
			var graphQLString = JsonConvert.SerializeObject(request, Options.JsonSerializerSettings);
			using (var httpContent = new StringContent(graphQLString, Encoding.UTF8, Options.MediaTypeHeaderValue.MediaType))
			using (var httpResponseMessage = await httpClient.PostAsync(Options.EndPoint, httpContent, cancellationToken).ConfigureAwait(false)) {
				return await ReadHttpResponseMessageAsync(httpResponseMessage).ConfigureAwait(false);
			}
		}

		public void Dispose() => httpClient.Dispose();

		async Task<GraphQLResponse> ReadHttpResponseMessageAsync(HttpResponseMessage httpResponseMessage)
		{
			using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
			using (var streamReader = new StreamReader(stream))
			using (var jsonTextReader = new JsonTextReader(streamReader)) {
				var jsonSerializer = new JsonSerializer {
					ContractResolver = Options.JsonSerializerSettings.ContractResolver
				};
				try {
					return jsonSerializer.Deserialize<GraphQLResponse>(jsonTextReader);
				}
				catch (JsonReaderException exception) {
					if (httpResponseMessage.IsSuccessStatusCode) {
						throw exception;
					}
					throw new GraphQLHttpException(httpResponseMessage);
				}
			}
		}
	}
}