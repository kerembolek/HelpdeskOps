INSERT INTO AspNetUsers
(Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
 SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled,
 LockoutEnabled, AccessFailedCount, FirstName, LastName, Department)
VALUES
('dev-user-1', 'dev@helpdeskops.local', 'DEV@HELPDESKOPS.LOCAL',
 'dev@helpdeskops.local', 'DEV@HELPDESKOPS.LOCAL', 1,
 NEWID(), NEWID(), 0, 0, 0, 0, 'Test', 'Kullanıcı', 'Bilgi Teknolojileri');