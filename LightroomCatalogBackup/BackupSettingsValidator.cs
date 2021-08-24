using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;

namespace LightroomCatalogBackup
{
    internal class BackupSettingsValidator : AbstractValidator<IBackupSettings>
    {
        public BackupSettingsValidator()
        {
            RuleFor(b => b.GlobalBackupDirectory)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(Directory.Exists).WithMessage("{PropertyName} must be an existing directory.");

            RuleFor(b => b.Catalogs)
                .Cascade(CascadeMode.Stop)
                .NotNull();

            RuleForEach(b => b.Catalogs)
                .SetValidator(new LightroomCatalogValidator());
        }
    }
}
