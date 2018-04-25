// MIT License
//   
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Xamarin.Forms.GraphQL.Client;
using System.Runtime.CompilerServices;

namespace Xamarin.Forms.GraphQL
{
	public class GraphQLObject : IGraphQLObject, INotifyPropertyChanged, IEnumerable, INotifyCollectionChanged, IList
	{
		Delayer _helper;
		Uri _endPoint;
		public Uri EndPoint {
			get => _endPoint;
			set {
				if (SetProperty(ref _endPoint, value)) {
					if (value == null) {
						_helper.Cancel();
						_helper = null;
					}
					else
						(_helper = new Delayer { Do = DoQuery }).Queue();
				}
			}
		}

		IGraphQLField _field;
		IGraphQLField _lastField;
		IEnumerable<string> _bindingPath;
		public IGraphQLField Field {
			get => _field;
			set {
				if (SetProperty(ref _field, value)) {
					_bindingPath = value.GetBindingPath();
					_lastField = value.GetLastField();
					_helper?.Queue();
				}
			}
		}

		IGraphQLObject _parentGQLObject;
		internal IGraphQLObject ParentGQLObject {
			get => _parentGQLObject;
			set {
				if (_parentGQLObject == value)
					return;

				if (this == value)
					throw new InvalidOperationException("Trying to set the parent to self");

				if (_parentGQLObject != null)
					_parentGQLObject.PropertyChanged -= OnParentGQLObjectPropertyChanged;

				_parentGQLObject = value;
				if (_field != null)
					_parentGQLObject?.AddQueryField(_field);
				if (_variables != null)
					foreach (var variable in _variables)
						_parentGQLObject?.AddVariable(variable);
				if (_parentGQLObject != null)
					_parentGQLObject.PropertyChanged += OnParentGQLObjectPropertyChanged;
			}
		}

		void OnParentGQLObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(IGraphQLObject.Data))
				return;
			var gqlParent = sender as IGraphQLObject;

			var data = gqlParent.Data;
			if (_bindingPath != null)
				foreach (var part in _bindingPath)
					data = ((JObject)data)[part];
			Data = data;
		}

		void IGraphQLObject.AddQueryField(IGraphQLField subfield)
		{
			((_lastField ?? (Field = new GraphQLField(null)))?.SubFields ?? (_lastField.SubFields = new List<IGraphQLField>())).Add(subfield);
			ParentGQLObject?.AddQueryField(_field);
			_helper?.Queue();
		}

		IList<IGraphQLVariable> _variables;
		void IGraphQLObject.AddVariable(IGraphQLVariable variable)
		{
			(_variables ?? (_variables = new List<IGraphQLVariable>())).Add(variable);
			ParentGQLObject?.AddVariable(variable);
			variable.PropertyChanged += (o, e) => _helper.Queue();
			_helper?.Queue();
		}

		object _data;
		public object Data {
			get => _data;
			protected set {
				if (SetProperty(ref _data, value))
					_collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}


		protected virtual string SerializeQuery(IGraphQLField field)
			=> field.Serialize("query", variables: _variables);

		protected virtual string SerializeVariables(IEnumerable<IGraphQLVariable> variables)
			=> variables.Serialize();

		protected virtual async Task<GraphQLResponse> PostQueryAsync(Uri endPoint, string jsonRequest, string jsonVariables, CancellationToken cancellationToken)
		{
			using (var client = new GraphQLClient(endPoint))
				return await client.PostQueryAsync(jsonRequest, jsonVariables, cancellationToken).ConfigureAwait(false);
		}

		protected virtual void OnResponse(GraphQLResponse response)
		{
			if (response.Errors != null)
				OnErrors(response.Errors);
			var data = response.Data;
			if (_bindingPath != null)
				foreach (var part in _bindingPath)
					data = ((JObject)data)?[part];
			Data = data;
		}

		protected virtual void OnErrors(GraphQLError[] errors)
		{
			foreach (var error in errors)
				System.Diagnostics.Debug.WriteLine(error.Message);
		}

		async Task DoQuery(CancellationToken cancellationToken)
		{
			if (_field == null)
				return;
			OnResponse(await PostQueryAsync(EndPoint, SerializeQuery(_field), SerializeVariables(_variables), cancellationToken).ConfigureAwait(false));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

		protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
				return false;
			storage = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		protected bool SetIndexer<TKey, TValue>(IDictionary<TKey, TValue> storage, TKey key, TValue value, [CallerMemberName] string propName = null)
		{
			if (storage.TryGetValue(key, out TValue existing) && Equals(existing, value))
				return false;
			storage[key] = value;
			OnPropertyChanged($"{propName}[{key}]");
			return true;
		}

		//IEnumerable, IList and INCC implementation required for being a ListView source
		IEnumerator IEnumerable.GetEnumerator()
			=> (Data is JArray enumerable) ? enumerable.GetEnumerator() : Enumerable.Empty<object>().GetEnumerator();

		int ICollection.Count
			=> (Data as JArray)?.Count ?? 0;

		object IList.this[int index] {
			get => (Data is JArray array) ? new { Data = array[index]} : null;
			set => throw new NotImplementedException();
		}

		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => throw new NotImplementedException();
		bool ICollection.IsSynchronized => throw new NotImplementedException();
		object ICollection.SyncRoot => throw new NotImplementedException();
		int IList.Add(object value) => throw new NotImplementedException();
		void IList.Clear() => throw new NotImplementedException();
		bool IList.Contains(object value) => throw new NotImplementedException();
		int IList.IndexOf(object value) => throw new NotImplementedException();
		void IList.Insert(int index, object value) => throw new NotImplementedException();
		void IList.Remove(object value) => throw new NotImplementedException();
		void IList.RemoveAt(int index) => throw new NotImplementedException();
		void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException();

		event NotifyCollectionChangedEventHandler _collectionChanged;
		event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
			add { _collectionChanged += value; }
			remove { _collectionChanged -= value; }
		}
	}
}