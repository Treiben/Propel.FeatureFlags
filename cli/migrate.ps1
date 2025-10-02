& ".\release\propel-cli.exe" migrate `
    --provider postgresql `
    --connection-string "Host=localhost;Port=5432;Database=propel_feature_flags;Search Path=dashboard;Username=propel_user;Password=propel_password;Include Error Detail=true" `
    --migrations-path ".\scripts\postgresql\migrations"