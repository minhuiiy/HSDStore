using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CPTStore.Services.Interfaces;
using CPTStore.Extensions;

namespace CPTStore.Filters
{
    public class CartItemCountFilter : IActionFilter
    {
        private readonly ICartService _cartService;

        public CartItemCountFilter(ICartService cartService)
        {
            _cartService = cartService;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is Controller controller)
            {
                var userId = context.HttpContext.Session.GetUserIdOrSessionId(context.HttpContext.User);
                var cartItemCount = _cartService.GetCartItemsCountAsync(userId).GetAwaiter().GetResult();
                controller.ViewBag.CartItemCount = cartItemCount;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No action needed after execution
        }
    }
}