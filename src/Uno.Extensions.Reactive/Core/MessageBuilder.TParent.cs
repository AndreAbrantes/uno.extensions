﻿using System;
using System.Collections.Generic;
using System.Linq;
using Uno.Extensions.Reactive.Core;
using Uno.Extensions.Reactive.Utils;

namespace Uno.Extensions.Reactive;

/// <summary>
/// A builder of <see cref="Message{T}"/> based on a parent message of <typeparamref name="TParent"/>.
/// </summary>
/// <typeparam name="TParent">Type of the value of the parrent message.</typeparam>
/// <typeparam name="TResult">The type of the value of the message to build.</typeparam>
public readonly struct MessageBuilder<TParent, TResult> : IMessageEntry, IMessageBuilder, IMessageBuilder<TResult>
{
	private readonly Dictionary<MessageAxis, MessageAxisUpdate> _updates;

	internal MessageBuilder(
		Message<TParent>? parent,
		(IReadOnlyDictionary<MessageAxis, MessageAxisUpdate> updates, Message<TResult> value) local)
	{
		Parent = parent;
		Local = local.value;

		_updates = local.updates.ToDictionary();
	}

	/// <summary>
	/// The last message received from the parent, if any.
	/// </summary>
	internal Message<TParent>? Parent { get; }

	/// <summary>
	/// The last message produced by the local Feed.
	/// </summary>
	internal Message<TResult> Local { get; }

	/// <summary>
	/// The new set of updates that has been defined on this builder
	/// </summary>
	internal (Message<TParent>? parent, IReadOnlyDictionary<MessageAxis, MessageAxisUpdate> updates) GetResult()
		=> (Parent, _updates);

	Option<object> IMessageEntry.Data => CurrentData;
	Exception? IMessageEntry.Error => CurrentError;
	bool IMessageEntry.IsTransient => CurrentIsTransient;
	MessageAxisValue IMessageEntry.this[MessageAxis axis] => Get(axis);

	MessageAxisValue IMessageBuilder.this[MessageAxis axis]
	{
		get => Get(axis);
		set => Set(axis, value);
	}

	internal Option<TResult> CurrentData => (Option<TResult>)Get(MessageAxis.Data).Value!;
	internal Exception? CurrentError => (Exception?)Get(MessageAxis.Data).Value;
	internal bool CurrentIsTransient => Get(MessageAxis.Progress) is { IsSet: true } progress && (bool)progress.Value!;
	internal MessageAxisValue this[MessageAxis axis]
	{
		get => Get(axis);
		set => Set(axis, value);
	}

	private MessageAxisValue Get(MessageAxis axis)
	{
		var parentValue = Parent?.Current[axis] ?? MessageAxisValue.Unset;
		var localValue = Local.Current[axis];
		if (_updates.TryGetValue(axis, out var updater))
		{
			return updater.GetValue(parentValue, localValue);
		}
		else
		{
			return parentValue;
		}
	}

	private void Set(MessageAxis axis, MessageAxisValue value, bool overridesParent = false)
	{
		// Note: We are not validating the axis.AreEquals as changes are detected by the MessageManager itself.

		_updates[axis] = new MessageAxisUpdate(axis, value){IsOverride = overridesParent};
	}
}
