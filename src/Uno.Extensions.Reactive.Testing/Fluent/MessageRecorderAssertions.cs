﻿using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using Uno.Extensions.Reactive.Testing;
using Uno.Extensions.Reactive;

namespace FluentAssertions;

public class MessageRecorderAssertions<T> : GenericCollectionAssertions<IFeedRecorder<T>, Message<T>, MessageRecorderAssertions<T>>
{
	/// <inheritdoc />
	public MessageRecorderAssertions(IFeedRecorder<T> actualValue)
		: base(actualValue)
	{
	}

	public void Be(Action<MessageRecorderConstraintBuilder<T>> constraintsBuilder)
	{
		var builder = new MessageRecorderConstraintBuilder<T>();
		constraintsBuilder(builder);
		var constraint = builder.Build();

		constraint.Assert(Subject);
	}
}
