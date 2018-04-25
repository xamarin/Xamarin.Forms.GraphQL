# Xamarin.Forms.GraphQL

{GraphQL} bindings allows binding from XAML directly to a GraphQL data source.

**WARNING: THIS WORK IS STILL IN PROGRESS. SOME CASES MIGHT NOT WORK AS EXPECTED, SOME FEATURES ARE DEFINITELY MISSING AND THE API IS SUBJECTIVE TO CHANGE.** That being said, we encourage you to play with it, use it, and send pull-requests.

# Querying using {GraphQL}
![](https://d2mxuefqeaa7sj.cloudfront.net/s_FE3BA36054D089C7FAA8CD0BE21CE6108546C3CFE92F4916C6600A6D8C975714_1521819687105_2018-03-23_1211.png)

Using {GraphQL} bindings, you declare and consume from the query at the same time. The previous sample have no code behind, no data model, no view model.

## 1. Setting the BindingContext

{GraphQL} bindings require a `BindingContext` of type `IGraphQLObject` to be set somewhere in the hierarchy. There are multiple ways to do so:

  **a.** use a {GraphQL} binding (like in the sample). that creates a default `GraphQLObject` and assign it to the BindingContext;
  **b.** explicitly set the `BindingContext` using xaml-element syntax
  **c.** assign from your own context implementing `IGraphQLObject`. That can be done by inheriting from, or compositing with, the `GraphQLObject`. `GraphQLObject` has been designed with those 2 patterns in mind, allowing query and response interception, so one could easily add a cache at that level.
  
        public interface IGraphQLObject : INotifyPropertyChanged
        {
            void AddQueryField(IGraphQLField field);
            void AddVariable(IGraphQLVariable variable);
            object Data { get; }
        }


        public class GraphQLObject : IGraphQLObject, INotifyPropertyChanged, IEnumerable, INotifyCollectionChanged, IList
        {
            public Uri EndPoint {get; set;}
            public IGraphQLField Field {get; set;}
            public object Data { get;protected set;}
            protected virtual string SerializeQuery(IGraphQLField field);
            protected virtual string SerializeVariables(IEnumerable<IGraphQLVariable> variables);
            protected virtual async Task<GraphQLResponse> PostQueryAsync(Uri endPoint, string jsonRequest, string jsonVariables, CancellationToken cancellationToken);
            protected virtual void OnResponse(GraphQLResponse response);
            protected virtual void OnErrors(GraphQLError[] errors);


## 2. Bind

Whenever you want to display a part of the query, instead of doing a `{Binding}`, you do `{GraphQL}`. You usually bind to a field, like in `{GraphQL openingCrawl}`.
This will request the `openingCrawl` field to be fetched from the parent field (`film` in this case). The query will eventually be fired (we queue the queries with a gracePeriod of 20ms, to avoid sending too many requests while the query is still being constructed), and a response will come back. At that moment, the `{GraphQL}` will work as a OneWay binding, and will display the data.

## 3. Nesting

GraphQL queries are all about require fields of fields. This nesting could be expressed in 2 ways using `{GraphQL}` bindings

  1. assign the `BindingContext` of the parent container, like in the sample (`title` is a field of the `film` field),
  2. use the `.`syntax. A single query could then be `{GraphQL homeworld.name}`.


## 4. Arguments

{GraphQL} queries can have arguments `{gql:GraphQL Field='film' Argument='id:&quot;ZmlsbXM6MQ==&quot;'}`. This will query a single film with the right `id`. It’s very convenient to use anything else that a fixed string for argument, like getting the `film.id` from somewhere else. For that purpose, arguments are bindable, and queries using bindable arguments uses GraphQL variables. In the sample at the top of this document, we do `Argument='id', ArgumentBinding={Binding Text, Source={x:Reference id}}`.

**Note**: I plan to change this syntax, and have `Argument=``'``id: bar``'` or `Argument={Argument id, ArgumentBinding={Binding …}, VariableType=ID}`. It makes more sense, at least to me.


## 5. Extra

It’s often useful to query additional, non-displayed and as such non-bound, field, like the `id` so you can use it further, or use it as a caching key. One way to do so is intercepting the query before sending to the server, the other way is requesting an `Extra` field, like `Extra=id`


## 6. Lists

ListView.ItemsSource can be bound to a collection part of the query (`vehicleConnection.vehicles`)

    <ContentPage BindingContext="{gql:GraphQL EndPoint='https://swapi.apis.guru/'}">
        <StackLayout BindingContext="{gql:GraphQL Field='person' Argument='personID:1'}">
            <Label Text="{gql:GraphQL name}" />
            <Label Text="Vehicles:" class="description" />
            <ListView ItemsSource="{gql:GraphQL vehicleConnection.vehicles}" VerticalOptions="End" >
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <ViewCell>
                            <Label Text="{gql:GraphQL name}" />
                        </ViewCell>
                    </DataTemplate>
                </ListView.ItemTemplate>
             </ListView>
        </StackLayout>
    </ContentPage>

Other custom controls can register themselves and their source and template properties so they will be handled like ListViews.

# Dependencies
- depends on XF 3.0.0-pre3
- NewtonSoft.Json
