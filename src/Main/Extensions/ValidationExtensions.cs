using System.ComponentModel.DataAnnotations;

// ReSharper disable once CheckNamespace
namespace System;

public static class ValidationExtensions
{
    public static TModel ThrowWhenNotValid<TModel>(this TModel model)
        where TModel : class, new()
    {
        var context = new ValidationContext(model);
        Validator.ValidateObject(model, context, validateAllProperties: true);

        return model;
    }
}
