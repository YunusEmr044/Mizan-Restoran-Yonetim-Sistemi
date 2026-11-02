using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class AdminFeedbackTests
{
    [Theory]
    [InlineData("Create", "Eklendi.")]
    [InlineData("HeroCreate", "Eklendi.")]
    [InlineData("AddOption", "Eklendi.")]
    [InlineData("DeleteConfirmed", "Silindi.")]
    [InlineData("HeroDelete", "Silindi.")]
    [InlineData("DeleteRecipeItem", "Silindi.")]
    [InlineData("ToggleActive", "Güncellendi.")]
    [InlineData("SetStatus", "Güncellendi.")]
    [InlineData("UpdateOrder", "Güncellendi.")]
    [InlineData("RenewQr", "Güncellendi.")]
    [InlineData("Edit", "Kaydedildi.")]
    [InlineData("SaveRolePermissions", "Kaydedildi.")]
    public void Success_Message_Follows_Action_Name(string action, string expected)
    {
        Assert.Equal(expected, AdminFeedback.SuccessMessageFor(action));
    }

    [Fact]
    public void Foreign_Key_Message_Explains_Why_Delete_Failed()
    {
        Assert.Contains("silinemez", AdminFeedback.ForeignKeyMessage("DeleteConfirmed"));
        Assert.Contains("kaydedilemedi", AdminFeedback.ForeignKeyMessage("Edit"));
    }

    private sealed class Sample
    {
        [Required]
        [StringLength(10)]
        [Display(Name = "Ad")]
        public string Name { get; set; } = "";

        [Range(1, 5)]
        public int Score { get; set; }
    }

    private static List<ValidationAttribute> Apply(string property)
    {
        var prop = typeof(Sample).GetProperty(property)!;
        var attributes = ModelAttributes.GetAttributesForProperty(typeof(Sample), prop);
        var context = new ValidationMetadataProviderContext(ModelMetadataIdentity.ForProperty(prop, prop.PropertyType, typeof(Sample)), attributes);
        foreach (var a in attributes.Attributes.OfType<ValidationAttribute>())
        {
            context.ValidationMetadata.ValidatorMetadata.Add(a);
        }
        new TurkishValidationMessages().CreateValidationMetadata(context);
        return context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>().ToList();
    }

    [Fact]
    public void Default_Validation_Messages_Are_Turkish()
    {
        var name = Apply(nameof(Sample.Name));
        Assert.Equal("Ad gereklidir.", name.OfType<RequiredAttribute>().Single().FormatErrorMessage("Ad"));
        Assert.Equal("Ad en fazla 10 karakter olabilir.", name.OfType<StringLengthAttribute>().Single().FormatErrorMessage("Ad"));

        var score = Apply(nameof(Sample.Score)).OfType<RangeAttribute>().Single();
        Assert.Equal("Bu alan 1 ile 5 arasında olmalıdır.", score.FormatErrorMessage("Score"));
    }

    [Fact]
    public void Explicit_Messages_Are_Kept()
    {
        var attr = new RequiredAttribute { ErrorMessage = "Özel mesaj." };
        var prop = typeof(Sample).GetProperty(nameof(Sample.Name))!;
        var context = new ValidationMetadataProviderContext(ModelMetadataIdentity.ForProperty(prop, prop.PropertyType, typeof(Sample)), ModelAttributes.GetAttributesForProperty(typeof(Sample), prop));
        context.ValidationMetadata.ValidatorMetadata.Add(attr);
        new TurkishValidationMessages().CreateValidationMetadata(context);
        Assert.Equal("Özel mesaj.", attr.ErrorMessage);
    }

    private static List<string> Errors(object model, string? member = null)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.Where(r => member == null || r.MemberNames.Contains(member)).Select(r => r.ErrorMessage ?? "").ToList();
    }

    [Fact]
    public void Campaign_Rejects_Percent_Over_100_And_End_Before_Start()
    {
        var errors = Errors(new Campaign { Name = "X", DiscountType = CampaignDiscountType.Yuzde, Value = 150, StartDate = new DateTime(2026, 12, 1), EndDate = new DateTime(2026, 11, 1) });
        Assert.Contains(errors, e => e.Contains("100"));
        Assert.Contains(errors, e => e.Contains("Bitiş"));
    }

    [Fact]
    public void Campaign_Allows_Fixed_Amount_Over_100()
    {
        Assert.Empty(Errors(new Campaign { Name = "X", DiscountType = CampaignDiscountType.SabitTutar, Value = 150 }));
    }

    [Fact]
    public void Negative_Amounts_Are_Rejected()
    {
        Assert.NotEmpty(Errors(new Campaign { Name = "X", Value = -1 }));
        Assert.Equal(2, Errors(new InventoryItem { Name = "X", Unit = "adet", MinLevel = -3, UnitCost = -2 }).Count);
        Assert.NotEmpty(Errors(new ChefSpecial { PriceOverride = -1 }, nameof(ChefSpecial.PriceOverride)));
        Assert.NotEmpty(Errors(new Customer { Name = "X", Phone = "5", Points = -1 }, nameof(Customer.Points)));
    }
}
