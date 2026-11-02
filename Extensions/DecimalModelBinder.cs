using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestoranYonetim.Services;

namespace RestoranYonetim.Extensions;

// decimal/decimal? form alanları kültürden bağımsız (DecimalInput) okunur; Program.cs'te ModelBinderProviders'ın başına eklenir.
public class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = context.Metadata.UnderlyingOrModelType;
        return type == typeof(decimal) ? new DecimalModelBinder() : null;
    }
}

public class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var raw = result.FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            return Task.CompletedTask;
        }

        if (DecimalInput.TryParse(raw, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Geçerli bir tutar girin (ör. 12,50).");
        }
        return Task.CompletedTask;
    }
}
