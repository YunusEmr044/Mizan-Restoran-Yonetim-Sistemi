using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace RestoranYonetim.Extensions;

// Kendi mesajı olmayan doğrulama kurallarına Türkçe varsayılan mesaj verir (çerçevenin İngilizce varsayılanları yerine).
public sealed class TurkishValidationMessages : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        var named = context.Attributes.Any(a => a is DisplayAttribute { Name: not null } or DisplayNameAttribute);
        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage != null || attribute.ErrorMessageResourceName != null)
            {
                continue;
            }
            if (MessageFor(attribute, named) is { } message)
            {
                attribute.ErrorMessage = message;
            }
        }
    }

    // Görünen adı olmayan alanlarda {0} kod içindeki alan adı olur; onun yerine "Bu alan" yazılır.
    public static string? MessageFor(ValidationAttribute attribute, bool named)
    {
        var message = attribute switch
        {
            RequiredAttribute => named ? "{0} gereklidir." : "Zorunlu alanları doldurun.",
            StringLengthAttribute { MinimumLength: > 0 } => "{0} {2} ile {1} karakter arasında olmalıdır.",
            StringLengthAttribute or MaxLengthAttribute => "{0} en fazla {1} karakter olabilir.",
            MinLengthAttribute => "{0} en az {1} karakter olmalıdır.",
            RangeAttribute => "{0} {1} ile {2} arasında olmalıdır.",
            EmailAddressAttribute => "Geçerli bir e-posta adresi girin.",
            PhoneAttribute => "Geçerli bir telefon numarası girin.",
            UrlAttribute => "Geçerli bir web adresi girin.",
            System.ComponentModel.DataAnnotations.CompareAttribute => "{0} ile {1} eşleşmiyor.",
            _ => null
        };
        return named ? message : message?.Replace("{0}", "Bu alan");
    }

    public static void Configure(MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new TurkishValidationMessages());
        var m = options.ModelBindingMessageProvider;
        m.SetValueMustBeANumberAccessor(field => $"{field} bir sayı olmalıdır.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "Değer bir sayı olmalıdır.");
        m.SetAttemptedValueIsInvalidAccessor((value, field) => $"'{value}' değeri {field} için geçerli değil.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' geçerli bir değer değil.");
        m.SetUnknownValueIsInvalidAccessor(field => $"{field} için girilen değer geçerli değil.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Girilen değer geçerli değil.");
        m.SetValueIsInvalidAccessor(value => $"'{value}' geçerli bir değer değil.");
        m.SetValueMustNotBeNullAccessor(_ => "Zorunlu alanları doldurun.");
        m.SetMissingBindRequiredValueAccessor(field => $"{field} için değer girilmedi.");
        m.SetMissingKeyOrValueAccessor(() => "Değer gereklidir.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "İstek boş gönderildi.");
    }
}
