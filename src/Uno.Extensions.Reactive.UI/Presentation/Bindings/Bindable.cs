﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Uno.Logging;

namespace Uno.Extensions.Reactive.Bindings;

/// <summary>
/// An helper class use to data-bind a value.
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public class Bindable<T> : IBindable, INotifyPropertyChanged
{
	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private T? _value;
	private CancellationTokenSource? _asyncSetCt;
	private List<Action<T?>>? _onUpdated;

	private readonly BindablePropertyInfo<T> _property;
	private readonly bool _hasValueProperty;

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="property">Info of the property that is backed by this instance.</param>
	/// <param name="hasValueProperty">
	/// Indicates if this instance has a property name Value which can be data bind directly instead of <see cref="GetValue"/> and <see cref="SetValue"/>.
	/// Is so, the <see cref="PropertyChanged"/> will be raise accordingly.
	/// </param>
	/// <exception cref="ArgumentException">If the property is invalid</exception>
	public Bindable(BindablePropertyInfo<T> property, bool hasValueProperty = false)
	{
		if (!property.IsValid)
		{
			throw new ArgumentException("Property IsInvalid, it must be created from a BindableViewModel or a Bindable", nameof(property));
		}

		_property = property;
		_hasValueProperty = hasValueProperty;

		property.Subscribe(OnOwnerUpdated);
	}

	/// <summary>
	/// Get info for a bindable sub-property.
	/// </summary>
	/// <typeparam name="TProperty">The type of the sub-property.</typeparam>
	/// <param name="propertyName">The name of the sub-property.</param>
	/// <param name="get">The getter.</param>
	/// <param name="set">The setter.</param>
	/// <returns>Info that can be used to create a sub-bindable object.</returns>
	protected BindablePropertyInfo<TProperty> Property<TProperty>(
		string propertyName,
		Func<T?, TProperty?> get,
		Func<T?, TProperty?, T> set)
		=> new(
			this,
			propertyName,
			updated =>
			{
				(_onUpdated ??= new()).Add(value => updated(get(value)));
				updated(get(_value)); // Make sure to propagate the default value
			},
			(update, isLeafPropertyChanged, ct) => OnSubPropertyUpdated(propertyName, get, set, update, isLeafPropertyChanged, ct));

	/// <summary>
	/// Gets the current value.
	/// </summary>
	/// <returns>The current value.</returns>
	public T? GetValue()
		=> _value;

	/// <summary>
	/// Sets the current value.
	/// </summary>
	/// <param name="value">The current value.</param>
	public void SetValue(T? value)
	{
		if (SetValueCore(value))
		{
			UpdateOwner(value);
		}
	}

	private bool SetValueCore(T? value)
	{
		if (object.ReferenceEquals(_value, value))
		{
			return false;
		}

		_value = value;

		// 1. Notify sub properties that the local value has changed
		UpdateSubProperties(value);

		// 2. Notify UI that the local value has changed
		if (_hasValueProperty)
		{
			// The Value property is generated only if the de-normalized entity does not already have a Value property.
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
		// Support for `{x:Bind GetValue()}`
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GetValue)));

		return true;
	}

	private void OnOwnerUpdated(T? value)
	{
		SetValueCore(value);
	}

	private async void UpdateOwner(T? value)
	{
		try
		{
			_asyncSetCt?.Cancel();
			_asyncSetCt = new();

			await _property.Update(_ => value, true, _asyncSetCt.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception error)
		{
			this.Log().Error(
				$"Synchronization from View to ViewModel of '{_property}' failed. "
				+ "(This is a temporary error, it will be retried on next change from the View.)",
				error);
		}
	}

	private async ValueTask OnSubPropertyUpdated<TProperty>(
		string propertyName,
		Func<T?, TProperty?> get,
		Func<T?, TProperty?, T> set,
		Func<TProperty?, TProperty?> update,
		bool isLeafPropertyChanged,
		CancellationToken ct)
	{
		// First updates the local value (and possibly other sub-properties that are dependent upon computed values)
		if (SetValueCore(set(_value, update(get(_value)))))
		{
			// If we effectively updated the local value, then ...

			// 1. If the sub property is a leaf property (or a complex value has been set using the SetValue on the property),
			//	  we need to notify that the property itself has changed.
			//	  (If we raise the event in other cases, we will actually "invalidate" the whole object tree)
			if (isLeafPropertyChanged)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}

			// 2. Propagate the update to the parent (i.e. update the backing state)
			// Note: We re-invoke the 'update' delegate here (instead of using the '_value') to make sure to respect ACID
			await _property.Update(t => set(t, update(get(t))), false, ct);
		}
	}

	private void UpdateSubProperties(T? value)
	{
		if (_onUpdated is not null)
		{
			foreach (var callback in _onUpdated)
			{
				callback(value);
			}
		}
	}
}
