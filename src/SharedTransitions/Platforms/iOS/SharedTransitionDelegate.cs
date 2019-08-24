﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ObjCRuntime;
using UIKit;

namespace Plugin.SharedTransitions.Platforms.iOS
{
	public class SharedTransitionDelegate : UINavigationControllerDelegate
	{
		/*
		 * IMPORTANT NOTES:
		 * Read the dedicate comments in code for more info about those fixes.
		 *
		 * Listview/collection view hidden item:
		 * Fix First item is created two times, then discarded and Detach not called
		 *
		 * Custom edge gesture recognizer:
		 * I need to enable/disable the standard edge swipe when needed
		 * because the custom one works well with transition but not so much without
		 */

		readonly IUINavigationControllerDelegate _oldDelegate;
		readonly ITransitionRenderer _self;

		public SharedTransitionDelegate(IUINavigationControllerDelegate oldDelegate, ITransitionRenderer renderer)
		{
			_oldDelegate = oldDelegate;
			_self        = renderer;
		}

		public override void DidShowViewController(UINavigationController navigationController, [Transient] UIViewController viewController, bool animated)
		{
			_oldDelegate?.DidShowViewController(navigationController, viewController, animated);
		}

		public override void WillShowViewController(UINavigationController navigationController, [Transient] UIViewController viewController, bool animated)
		{
			_oldDelegate?.WillShowViewController(navigationController, viewController, animated);
		}

		public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForOperation(UINavigationController navigationController, UINavigationControllerOperation operation, UIViewController fromViewController, UIViewController toViewController)
		{
			if (!_self.DisableTransition)
			{
				//At this point the property TargetPage refers to the view we are pushing or popping
				//This view is not yet visible in our app but the variable is already set
				var viewsToAnimate = new List<(UIView ToView, UIView FromView)>();

				IReadOnlyList<TransitionDetail> transitionStackTo;
				IReadOnlyList<TransitionDetail> transitionStackFrom;

				if (operation == UINavigationControllerOperation.Push)
				{
					transitionStackFrom = _self.TransitionMap.GetMap(_self.PropertiesContainer, _self.SelectedGroup);
					transitionStackTo = _self.TransitionMap.GetMap(_self.LastPageInStack, null);
				}
				else
				{
					//During POP, everyting is fine and clear
					transitionStackFrom = _self.TransitionMap.GetMap(_self.LastPageInStack, null);
					transitionStackTo = _self.TransitionMap.GetMap(_self.PropertiesContainer, _self.SelectedGroup);
				}

				if (transitionStackFrom != null)
				{
					//Get all the views with transitions in the destination page
					//With this, we are sure to dont start transitions with no mathing transitions in destination
					foreach (var transitionToMap in transitionStackTo)
					{
						var toView = toViewController.View.ViewWithTag(transitionToMap.NativeViewId);
						if (toView != null)
						{
							//get the matching transition: we store the destination view and the corrispondent transition in the source view,
							//so we can match them during transition.

							/*
							 * IMPORTANT
							 *
							 * Using ListView/Collection, the first item is created two times, but then one of them is discarded
							 * without calling the Detach method from our effect. So we need to find the right element!
							 */

							foreach (var nativeView in transitionStackFrom
								.Where(x => x.TransitionName == transitionToMap.TransitionName)
								.OrderByDescending(x => x.NativeViewId))
							{
								var fromView = fromViewController.View.ViewWithTag(nativeView.NativeViewId);
								if (fromView != null)
								{
									viewsToAnimate.Add((toView, fromView));
									break;
								}
							}
						}
						else
						{
							Debug.WriteLine($"The destination ViewId {transitionToMap.NativeViewId} has no corrisponding Navive Views in tree");
						}
					}
				}

				//IF we have views to animate, proceed with custom transition and edge gesture
				//No view to animate = standard push & pop
				if (viewsToAnimate.Any())
				{
					//deactivate normal pop gesture and activate the custom one suited for the shared transitions
					if (operation == UINavigationControllerOperation.Push)
						_self.AddInteractiveTransitionRecognizer();

					return new NavigationTransition(viewsToAnimate, operation, _self, _self.EdgeGestureRecognizer);
				}
			}

			/*
			 * IMPORTANT!
			 *
			 * standard push & pop
			 * i dont use my custom edgeswipe because it does not play well with standard pop
			 * Doing this work here, is good for push.
			 * When doing the custom, interactive, pop i need to double check the custom gesture later
			 * (see comments in UIGestureRecognizerState.Ended)
			 */

			_self.RemoveInteractiveTransitionRecognizer();
			return null;
		}

		public override IUIViewControllerInteractiveTransitioning GetInteractionControllerForAnimationController(
			UINavigationController navigationController, IUIViewControllerAnimatedTransitioning animationController)
		{
			return _self.PercentDrivenInteractiveTransition;
		}
	}
}