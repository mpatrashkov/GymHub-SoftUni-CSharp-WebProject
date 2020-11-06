﻿using GymHub.Data.Models;
using GymHub.Web.Models;
using GymHub.Web.Models.InputModels;
using GymHub.Web.Models.ViewModels;
using GymHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GymHub.Web.Controllers
{
    public class CartsController : Controller
    {
        private readonly ICartService cartService;
        private readonly UserManager<User> userManager;
        public CartsController(ICartService cartService, UserManager<User> userManager)
        {
            this.cartService = cartService;
            this.userManager = userManager;
        }

        [Authorize]
        public async Task<IActionResult> All()
        {
            var currentUserId = this.userManager.GetUserId(this.User);
            var productsInCart = await this.cartService.GetAllProductsFromCartAsync(currentUserId);

            var complexModel = new ComplexModel<List<BuyProductInputModel>, List<ProductCartViewModel>>
            {
                ViewModel = productsInCart
            };

            return this.View(complexModel);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Buy(ComplexModel<List<BuyProductInputModel>, List<ProductCartViewModel>> complexModel)
        {
            throw new NotImplementedException("Ne sum go vkaral tova");
            return this.View("../Home/Index", complexModel);
        }

        [Authorize]
        public async Task<IActionResult> AddToCart(string productId, int quantity)
        {
            var currentUserId = this.userManager.GetUserId(this.User);
            await this.cartService.AddToCartAsync(productId, currentUserId, quantity);

            return this.Redirect("/Carts/All");
        }
    }
}
