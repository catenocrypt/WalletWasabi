using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectedIndicatorState : IDisposable
	{
		public readonly ConcurrentDictionary<int, Control> ScopeChildren = new();
		public Rectangle? PreviousIndicator { get; set; }
		public NavBarSelectionIndicatorAdorner AdornerControl { get; set; }

		public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

		public void Dispose()
		{
			ScopeChildren?.Clear();
		}

		public void AddChild(Control associatedObject)
		{
			if (ScopeChildren.ContainsKey(associatedObject.GetHashCode()))
			{
				return;
			}

			ScopeChildren.TryAdd(associatedObject.GetHashCode(),
				associatedObject);
		}

		public void Animate(Rectangle NextIndicator)
		{
			// For Debouncing.
			if (PreviousIndicator == NextIndicator)
			{
				return;
			}

			var root = PreviousIndicator.GetVisualAncestors().OfType<VisualLayerManager>().FirstOrDefault();

			_currentAnimationCts?.Cancel();
			AdornerControl.AnimateIndicators(PreviousIndicator, NextIndicator,
				_currentAnimationCts.Token, NavItemsOrientation);
			_currentAnimationCts = new();

			PreviousIndicator = NextIndicator;
		}

		private CancellationTokenSource _currentAnimationCts = new();
	}
}
