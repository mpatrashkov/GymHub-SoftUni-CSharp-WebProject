﻿using GymHub.Data.Data;
using GymHub.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GymHub.Services
{
    public class ProductCommentService : IProductCommentService
    {
        private readonly ApplicationDbContext context;
        private readonly ProductService productService;

        public ProductCommentService(ApplicationDbContext context)
        {
            this.context = context;
        }

        public ProductCommentService(ApplicationDbContext context, ProductService productService)
        {
            this.context = context;
            this.productService = productService;
        }
        public async Task AddAsync(ProductComment productComment)
        {
            await this.context.AddAsync(productComment);
            await this.context.SaveChangesAsync();
        }

        public async Task<bool> CommentExists(ProductComment productComment)
        {
            if (this.context.ProductsComments.Any(x => x.Id == productComment.Id) == true)
                return true;
            if (this.context.ProductsComments.Any(
                x => x.ParentCommentId == productComment.ParentCommentId && productComment.ParentCommentId == null && x.ProductId == productComment.ProductId && x.UserId == productComment.UserId) == true)
            {
                return true;
            }

            return false;
        }

        public bool CommentExists(string commentId)
        {
            return this.context.ProductsComments.Any(x => x.Id == commentId);
        }

        public async Task<List<ProductComment>> GetAllChildCommentsAsync(ProductComment productComment)
        {
            var children = this.context.ProductsComments
                .Where(x => x.ParentCommentId == productComment.Id)
                .ToList();

            var currentChildrenCount = children.Count;

            for (int i = 0; i < currentChildrenCount; i++)
            {
                var child = children[i];
                var childrenToChildren = await GetAllChildCommentsAsync(child);
                children.AddRange(childrenToChildren);
            }

            return children;
        }

        public ProductComment GetProductComment(string commentId)
        {
            return this.context.ProductsComments.FirstOrDefault(x => x.Id == commentId);
        }

        public async Task EditCommentText(ProductComment comment, string text)
        {
            if (comment != null) comment.Text = text;
            await this.context.SaveChangesAsync();
        }

        public bool CommentMatchesUserAndProduct(string commentId, string userId, string productId)
        {
            var comment = this.context.ProductsComments.FirstOrDefault(x => x.Id == commentId);

            if (comment == null) return false;

            return comment.UserId == userId && comment.ProductId == productId;
        }

        public Task EditCommentTextAsync(ProductComment comment, string text)
        {
            throw new NotImplementedException();
        }

        public async Task RemoveAsync(string commentId)
        {
            var removedComment = this.context.ProductsComments.FirstOrDefault(x => x.Id == commentId);
            removedComment.IsDeleted = true;
            removedComment.DeletedOn = DateTime.UtcNow;

            if (this.productService.ProductRatingExists(removedComment.UserId, removedComment.ProductId))
            {
                var ratingFromComment = this.productService.GetProductRating(removedComment.UserId, removedComment.ProductId);
                ratingFromComment.IsDeleted = true;
                ratingFromComment.DeletedOn = DateTime.UtcNow;
            }

            await this.context.SaveChangesAsync();
        }
    }
}
