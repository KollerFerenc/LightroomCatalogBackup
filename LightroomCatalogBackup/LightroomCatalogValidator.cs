using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;

namespace LightroomCatalogBackup
{
    internal class LightroomCatalogValidator : AbstractValidator<ILightroomCatalog>
    {
        public LightroomCatalogValidator()
        {
            RuleFor(l => l.PathToFile)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(IsLightroomCatalog).WithMessage("{PropertyName} must be an existing Lightroom catalog file.");

            RuleFor(l => l.CustomBackupDirectory)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(Directory.Exists)
                .When(l => l.HasCustomBackupDirectory).WithMessage("{PropertyName} must be an existing directory.");
        }

        protected bool FileExists(string file)
        {
            if (Directory.Exists(file))
                return false;

            return File.Exists(file);
        }

        protected bool IsLightroomCatalog(string file)
        {
            if (!FileExists(file))
                return false;

            return Path.GetExtension(file) == ".lrcat";
        }
    }
}
