﻿using AutoMapper;
using GymHub.Common;
using GymHub.Services;
using GymHub.Services.ServicesFolder.CartService;
using GymHub.Services.ServicesFolder.CountryService;
using GymHub.Services.ServicesFolder.PaymentMethodService;
using GymHub.Services.ServicesFolder.ProductService;
using GymHub.Services.ServicesFolder.SaleService;
using GymHub.Web.Helpers.NotificationHelpers;
using GymHub.Web.Models;
using GymHub.Web.Models.InputModels;
using GymHub.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GymHub.Web.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ISaleService saleService;
        private readonly IProductService productService;
        private readonly ICountryService countryService;
        private readonly IPaymentMethodService paymentMethodService;
        private readonly ICartService cartService;
        private readonly IUserService userService;
        private readonly IMapper mapper;
        private readonly SessionService sessionService;

        public SalesController
            (ISaleService saleService, IProductService productService, ICountryService countryService,
            IPaymentMethodService paymentMethodService, ICartService cartService, IUserService userService, IMapper mapper,
            SessionService sessionService)
        {
            this.saleService = saleService;
            this.productService = productService;
            this.countryService = countryService;
            this.paymentMethodService = paymentMethodService;
            this.cartService = cartService;
            this.userService = userService;
            this.mapper = mapper;
            this.sessionService = sessionService;
        }

        public async Task<IActionResult> Checkout()
        {
            var currentUserId = this.userService.GetUserId(this.User.Identity.Name);

            var cardProducts = this.cartService.GetAllProductsForCheckoutViewModel(currentUserId);

            //Check if the user has any products in their cart
            if (cardProducts.Count == 0)
            {
                //Set notification
                NotificationHelper.SetNotification(TempData, NotificationType.Error, "You do not have any products in your cart");

                return this.RedirectToAction("All", "Carts");
            }

            var checkoutViewModel = new CheckoutViewModel
            {
                ProductsInfo = cardProducts,
                TotalPrice = cardProducts.Sum(x => x.SinglePrice * x.Quantity),
                Countries = this.countryService.GetAllCountries(),
                PaymentMethods = this.paymentMethodService.GetAllPaymentMethods()
            };

            var complexModel = new ComplexModel<CheckoutInputModel, CheckoutViewModel>
            {
                ViewModel = checkoutViewModel
            };

            //TODO: Add input model on error from post request

            if(this.TempData[GlobalConstants.ErrorsFromPOSTRequest] != null)
            {
                ModelStateHelper.MergeModelStates(this.TempData, this.ModelState);
            }

            if(this.TempData[GlobalConstants.InputModelFromPOSTRequestType]?.ToString() == nameof(CheckoutInputModel))
            {
                complexModel.InputModel = JsonSerializer.Deserialize<CheckoutInputModel>(this.TempData[GlobalConstants.InputModelFromPOSTRequest]?.ToString());
            }

            return this.View(complexModel);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(ComplexModel<CheckoutInputModel, CheckoutViewModel> complexModel)
        {
            this.TempData[GlobalConstants.InputModelFromPOSTRequest] = JsonSerializer.Serialize(complexModel.InputModel);
            this.TempData[GlobalConstants.InputModelFromPOSTRequestType] = nameof(CheckoutInputModel);

            //Validate model without checking the database
            if(this.ModelState.IsValid == false)
            {
                //Serialize errors from modelstate
                this.TempData[GlobalConstants.ErrorsFromPOSTRequest] = ModelStateHelper.SerialiseModelState(this.ModelState);

                //Set notification
                NotificationHelper.SetNotification(this.TempData, NotificationType.Error, "An error occured while processing your request");

                return this.RedirectToAction(nameof(Checkout));
            }

            //Check if the payment method exists
            if(this.paymentMethodService.PaymentMethodExistsById(complexModel.InputModel.PaymentMethodId) == false)
            {
                this.ModelState.AddModelError("", "This payment method doesn't exist");

                //Serialize errors from modelstate
                this.TempData[GlobalConstants.ErrorsFromPOSTRequest] = ModelStateHelper.SerialiseModelState(this.ModelState);

                //Set notification
                NotificationHelper.SetNotification(this.TempData, NotificationType.Error, "Selected payment method doesn't exist");

                return this.RedirectToAction(nameof(Checkout));
            }

            var currentUserId = this.userService.GetUserId(this.User.Identity.Name);

            var cartProducts = this.cartService.GetAllProductsForCheckoutViewModel(currentUserId);

            var inputModel = complexModel.InputModel;

            //Check if the user has any products in their cart
            if (await this.cartService.GetNumberOfProductsInCart(currentUserId) == 0)
            {
                //Set notification
                NotificationHelper.SetNotification(TempData, NotificationType.Error, "You do not have any products in your cart");

                return this.RedirectToAction("All", "Carts");
            }

            //Check if the products' quantities exceed the amount of units in stock
            foreach (var product in cartProducts)
            {
                if(product.Quantity > product.QuantityInStock)
                {
                    this.ModelState.AddModelError("", $"{product.Name}:{product.Model} has less units available than you are currently trying to buy");

                    NotificationHelper.SetNotification(this.TempData, NotificationType.Error, $"{product.Name}:{product.Model} has less units available than you are currently trying to buy");
                }
            }

            if(this.ModelState.IsValid == false)
            {
                //Serialize errors from modelstate
                this.TempData[GlobalConstants.ErrorsFromPOSTRequest] = ModelStateHelper.SerialiseModelState(this.ModelState);

                return this.RedirectToAction(nameof(Checkout));
            }

            var paymentMethod = this.paymentMethodService.GetPaymentMethod(complexModel.InputModel.PaymentMethodId);

            if (paymentMethod == GlobalConstants.DebitOrCreditCard)
            {
                //Set the temp data for a successful sale
                var confirmSaleToken = SetTempDataForSale(complexModel.InputModel, cartProducts);

                //Setup Stripe payment session
                var sessionId = await CreateStripeSession(cartProducts, confirmSaleToken);

                //If it is null for whatever reason, then just return an error notification and redirect
                if (sessionId == null)
                {
                    //Clear temp data with purchase info
                    this.TempData.Clear();

                    //Set notification
                    NotificationHelper.SetNotification(this.TempData, NotificationType.Error, "Something went wrong with the purchase. Purchase reverted. You have not been charged.");

                    return this.RedirectToAction(nameof(Checkout));
                }

                return this.Json(new { id = sessionId, PaymentMethod = GlobalConstants.DebitOrCreditCard });
            }
            else if(paymentMethod == GlobalConstants.CashOnDelivery)
            {
                //Set the temp data for a successful sale
                var confirmSaleToken = SetTempDataForSale(complexModel.InputModel, cartProducts);

                return this.Json(new { PaymentMethod = GlobalConstants.CashOnDelivery, RedirectPath = $"/Sales/CheckoutSuccess?confirmSaleToken={confirmSaleToken}" });
            }

            //If we have gotten this far, then the payment method was one of the other two, which are not supported at this moment
            NotificationHelper.SetNotification(this.TempData, NotificationType.Error, "These two payments are not currently supported.");

            return this.Json(new { });
        }

        private string SetTempDataForSale(CheckoutInputModel saleInfo, List<CheckoutProductViewModel> products)
        {
            this.TempData[GlobalConstants.SaleInfo] = JsonSerializer.Serialize(saleInfo);
            this.TempData[GlobalConstants.SaleProductsInfo] = JsonSerializer.Serialize(products);
            var confirmSaleToken = Guid.NewGuid().ToString();
            this.TempData[GlobalConstants.ConfirmSaleToken] = confirmSaleToken;

            return confirmSaleToken;
        }

        private async Task<string> CreateStripeSession(List<CheckoutProductViewModel> products, string confirmSaleToken)
        {
            var currentDomain = $"{this.Request.Scheme}://{this.Request.Host}";
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                    {
                      "card",
                    },
                LineItems = products
                    .Select(product => new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(product.SinglePrice * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = product.Name,
                                Images = product.ImagesUrls.Select(image => {

                                    if (image.StartsWith("/")) return $"{currentDomain}{image}";
                                    else return image;

                                }).Take(8).ToList(),
                                Description = product.Description
                            },
                        },
                        Quantity = product.Quantity
                    }).ToList(),
                Mode = "payment",
                SuccessUrl = currentDomain + $"/Sales/CheckoutSuccess?confirmSaleToken={confirmSaleToken}",
                CancelUrl = currentDomain + $"/Sales/CheckoutCancel",
            };

            var session = await this.sessionService.CreateAsync(options, new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString() });
            return session.Id;
        }

        public async Task<IActionResult> CheckoutCancel()
        {
            //Clear sale temp data info
            this.TempData.Clear();

            NotificationHelper.SetNotification(this.TempData, NotificationType.Error, "You have canceled your payment");

            return this.RedirectToAction(nameof(Checkout));
        }

        public async Task<IActionResult> CheckoutSuccess(string confirmSaleToken)
        {
            if(this.TempData[GlobalConstants.ConfirmSaleToken]?.ToString() == confirmSaleToken)
            {
                var currentUserId = this.userService.GetUserId(this.User.Identity.Name);

                var saleInfo = JsonSerializer.Deserialize<CheckoutInputModel>(this.TempData[GlobalConstants.SaleInfo]?.ToString());
                var saleProductsInfo = JsonSerializer.Deserialize<List<CheckoutProductViewModel>>(this.TempData[GlobalConstants.SaleProductsInfo].ToString());

                await this.saleService.CheckoutAsync(saleInfo, currentUserId, saleProductsInfo);

                NotificationHelper.SetNotification(this.TempData, NotificationType.Success, "You successfully completed your order. You can review your order information in the profile tab");

                return this.RedirectToAction("All", "Carts");
            }
            else
            {
                return this.NotFound();
            }
        }

        public async Task<IActionResult> All()
        {
            return this.View();
        }
    }
}
