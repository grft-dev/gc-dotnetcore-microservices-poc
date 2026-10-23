IF DB_ID(N'Insurance') IS NULL
    CREATE DATABASE Insurance;
GO

USE Insurance;
GO

IF OBJECT_ID(N'Product', N'U') IS NULL
    CREATE TABLE Product (
        Code nvarchar(20) NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Image nvarchar(300) NULL,
        Description nvarchar(500) NULL,
        MaxNumberOfInsured int NOT NULL,
        Icon nvarchar(100) NULL,
        Status nvarchar(20) NOT NULL
    );
GO

IF OBJECT_ID(N'ProductCover', N'U') IS NULL
    CREATE TABLE ProductCover (
        ProductCode nvarchar(20) NOT NULL,
        Code nvarchar(20) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(500) NULL,
        Optional bit NOT NULL,
        SumInsured decimal(18,2) NULL,
        SortOrder int NOT NULL,
        CONSTRAINT PK_ProductCover PRIMARY KEY (ProductCode, Code)
    );
GO

IF OBJECT_ID(N'ProductQuestion', N'U') IS NULL
    CREATE TABLE ProductQuestion (
        ProductCode nvarchar(20) NOT NULL,
        Code nvarchar(50) NOT NULL,
        Kind nvarchar(20) NOT NULL,
        QuestionIndex int NOT NULL,
        Text nvarchar(300) NOT NULL,
        CONSTRAINT PK_ProductQuestion PRIMARY KEY (ProductCode, Code)
    );
GO

IF OBJECT_ID(N'ProductChoice', N'U') IS NULL
    CREATE TABLE ProductChoice (
        ProductCode nvarchar(50) NOT NULL,
        QuestionCode nvarchar(50) NOT NULL,
        Code nvarchar(50) NOT NULL,
        Label nvarchar(200) NOT NULL,
        CONSTRAINT PK_ProductChoice PRIMARY KEY (ProductCode, QuestionCode, Code)
    );
GO

IF OBJECT_ID(N'TariffBaseRule', N'U') IS NULL
    CREATE TABLE TariffBaseRule (
        ProductCode nvarchar(20) NOT NULL,
        SortOrder int NOT NULL,
        CoverCode nvarchar(20) NOT NULL,
        ApplyIfFormula nvarchar(500) NULL,
        BasePriceFormula nvarchar(500) NOT NULL,
        CONSTRAINT PK_TariffBaseRule PRIMARY KEY (ProductCode, SortOrder)
    );
GO

IF OBJECT_ID(N'TariffMarkupRule', N'U') IS NULL
    CREATE TABLE TariffMarkupRule (
        ProductCode nvarchar(20) NOT NULL,
        SortOrder int NOT NULL,
        ApplyIfFormula nvarchar(500) NULL,
        ParamValue decimal(18,4) NOT NULL,
        CONSTRAINT PK_TariffMarkupRule PRIMARY KEY (ProductCode, SortOrder)
    );
GO

IF OBJECT_ID(N'Offer', N'U') IS NULL
    CREATE TABLE Offer (
        Number nvarchar(50) NOT NULL PRIMARY KEY,
        ProductCode nvarchar(20) NOT NULL,
        PolicyFrom date NOT NULL,
        PolicyTo date NOT NULL,
        TotalPrice decimal(18,2) NOT NULL,
        Status nvarchar(20) NOT NULL,
        CreationDate datetime2 NOT NULL,
        AgentLogin nvarchar(100) NULL
    );
GO

IF OBJECT_ID(N'OfferCover', N'U') IS NULL
    CREATE TABLE OfferCover (
        OfferNumber nvarchar(50) NOT NULL,
        Code nvarchar(20) NOT NULL,
        Price decimal(18,2) NOT NULL,
        SortOrder int NOT NULL,
        CONSTRAINT PK_OfferCover PRIMARY KEY (OfferNumber, Code)
    );
GO

IF OBJECT_ID(N'Policy', N'U') IS NULL
    CREATE TABLE Policy (
        Number nvarchar(50) NOT NULL PRIMARY KEY,
        ProductCode nvarchar(20) NOT NULL,
        Status nvarchar(20) NOT NULL,
        CreationDate datetime2 NOT NULL,
        AgentLogin nvarchar(100) NULL
    );
GO

IF OBJECT_ID(N'PolicyVersion', N'U') IS NULL
    CREATE TABLE PolicyVersion (
        PolicyNumber nvarchar(50) NOT NULL,
        VersionNumber int NOT NULL,
        FirstName nvarchar(100) NOT NULL,
        LastName nvarchar(100) NOT NULL,
        TaxId nvarchar(50) NULL,
        Country nvarchar(100) NULL,
        ZipCode nvarchar(20) NULL,
        City nvarchar(100) NULL,
        Street nvarchar(200) NULL,
        CoverFrom date NOT NULL,
        CoverTo date NOT NULL,
        VersionFrom date NOT NULL,
        VersionTo date NOT NULL,
        TotalPremium decimal(18,2) NOT NULL,
        CONSTRAINT PK_PolicyVersion PRIMARY KEY (PolicyNumber, VersionNumber)
    );
GO

IF OBJECT_ID(N'PolicyCover', N'U') IS NULL
    CREATE TABLE PolicyCover (
        PolicyNumber nvarchar(50) NOT NULL,
        VersionNumber int NOT NULL,
        Code nvarchar(20) NOT NULL,
        Premium decimal(18,2) NOT NULL,
        ValidFrom date NOT NULL,
        ValidTo date NOT NULL,
        SortOrder int NOT NULL,
        CONSTRAINT PK_PolicyCover PRIMARY KEY (PolicyNumber, VersionNumber, Code)
    );
GO

IF OBJECT_ID(N'PolicyAccount', N'U') IS NULL
    CREATE TABLE PolicyAccount (
        PolicyNumber nvarchar(50) NOT NULL PRIMARY KEY,
        AccountNumber nvarchar(50) NOT NULL,
        FirstName nvarchar(100) NULL,
        LastName nvarchar(100) NULL,
        Status nvarchar(20) NOT NULL
    );
GO

IF OBJECT_ID(N'AccountEntry', N'U') IS NULL
    CREATE TABLE AccountEntry (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PolicyNumber nvarchar(50) NOT NULL,
        Kind nvarchar(20) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        EffectiveDate datetime2 NOT NULL,
        CreationDate datetime2 NOT NULL
    );
GO

IF OBJECT_ID(N'PolicySearch', N'U') IS NULL
    CREATE TABLE PolicySearch (
        PolicyNumber nvarchar(50) NOT NULL PRIMARY KEY,
        PolicyHolder nvarchar(200) NOT NULL,
        ProductCode nvarchar(20) NOT NULL,
        Status nvarchar(20) NOT NULL
    );
GO

IF OBJECT_ID(N'SalesFact', N'U') IS NULL
    CREATE TABLE SalesFact (
        PolicyNumber nvarchar(50) NOT NULL PRIMARY KEY,
        AgentLogin nvarchar(100) NULL,
        ProductCode nvarchar(20) NOT NULL,
        SalesDate date NOT NULL,
        Premium decimal(18,2) NOT NULL
    );
GO

IF OBJECT_ID(N'Agent', N'U') IS NULL
    CREATE TABLE Agent (
        Login nvarchar(100) NOT NULL PRIMARY KEY,
        Password nvarchar(100) NOT NULL,
        Avatar nvarchar(300) NULL,
        Products nvarchar(200) NOT NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM Product WHERE Code = N'TRI')
BEGIN
    INSERT INTO Product (Code, Name, Image, Description, MaxNumberOfInsured, Icon, Status)
    VALUES (N'TRI', N'Safe Traveller', N'/static/travel.jpg', N'Travel insurance', 10, N'plane', N'Active');
    INSERT INTO ProductCover (ProductCode, Code, Name, Description, Optional, SumInsured, SortOrder) VALUES
        (N'TRI', N'C2', N'Illness', N'', 0, 5000, 0),
        (N'TRI', N'C3', N'Assistance', N'', 1, NULL, 1);
    INSERT INTO ProductQuestion (ProductCode, Code, Kind, QuestionIndex, Text) VALUES
        (N'TRI', N'DESTINATION', N'choice', 1, N'Destination'),
        (N'TRI', N'NUM_OF_ADULTS', N'numeric', 2, N'Number of adults'),
        (N'TRI', N'NUM_OF_CHILDREN', N'numeric', 3, N'Number of children');
    INSERT INTO ProductChoice (ProductCode, QuestionCode, Code, Label) VALUES
        (N'TRI', N'DESTINATION', N'EUR', N'Europe'),
        (N'TRI', N'DESTINATION', N'WORLD', N'World'),
        (N'TRI', N'DESTINATION', N'PL', N'Poland');
    INSERT INTO TariffBaseRule (ProductCode, SortOrder, CoverCode, ApplyIfFormula, BasePriceFormula) VALUES
        (N'TRI', 1, N'C1', NULL, N'(NUM_OF_ADULTS) * (DESTINATION == "EUR" ? 26.00M : 34.00M)'),
        (N'TRI', 2, N'C2', NULL, N'(NUM_OF_ADULTS + NUM_OF_CHILDREN) * 26.00M'),
        (N'TRI', 3, N'C3', NULL, N'(NUM_OF_ADULTS + NUM_OF_CHILDREN) * 10.00M');
    INSERT INTO TariffMarkupRule (ProductCode, SortOrder, ApplyIfFormula, ParamValue) VALUES
        (N'TRI', 1, N'DESTINATION == "WORLD"', 1.5);
END
GO

IF NOT EXISTS (SELECT 1 FROM Product WHERE Code = N'HSI')
BEGIN
    INSERT INTO Product (Code, Name, Image, Description, MaxNumberOfInsured, Icon, Status)
    VALUES (N'HSI', N'Happy House', N'/static/house.jpg', N'House insurance', 5, N'building', N'Active');
    INSERT INTO ProductCover (ProductCode, Code, Name, Description, Optional, SumInsured, SortOrder) VALUES
        (N'HSI', N'C1', N'Fire', N'', 0, 200000, 0),
        (N'HSI', N'C2', N'Flood', N'', 0, 100000, 1),
        (N'HSI', N'C3', N'Theft', N'', 0, 50000, 2),
        (N'HSI', N'C4', N'Assistance', N'', 1, NULL, 3);
    INSERT INTO ProductQuestion (ProductCode, Code, Kind, QuestionIndex, Text) VALUES
        (N'HSI', N'TYP', N'choice', 1, N'Apartment / House'),
        (N'HSI', N'AREA', N'numeric', 2, N'Area'),
        (N'HSI', N'NUM_OF_CLAIM', N'numeric', 3, N'Number of claims in last 5 years'),
        (N'HSI', N'FLOOD', N'choice', 4, N'Located in flood risk area');
    INSERT INTO ProductChoice (ProductCode, QuestionCode, Code, Label) VALUES
        (N'HSI', N'TYP', N'APT', N'Apartment'),
        (N'HSI', N'TYP', N'HOUSE', N'House'),
        (N'HSI', N'FLOOD', N'YES', N'Yes'),
        (N'HSI', N'FLOOD', N'NO', N'No');
    INSERT INTO TariffBaseRule (ProductCode, SortOrder, CoverCode, ApplyIfFormula, BasePriceFormula) VALUES
        (N'HSI', 1, N'C1', N'TYP == "APT"', N'AREA * 1.25M'),
        (N'HSI', 2, N'C1', N'TYP == "HOUSE"', N'AREA * 1.50M'),
        (N'HSI', 3, N'C2', N'TYP == "APT"', N'AREA * 0.25M'),
        (N'HSI', 4, N'C2', N'TYP == "HOUSE"', N'AREA * 0.45M'),
        (N'HSI', 5, N'C3', NULL, N'30M'),
        (N'HSI', 6, N'C4', NULL, N'50M');
    INSERT INTO TariffMarkupRule (ProductCode, SortOrder, ApplyIfFormula, ParamValue) VALUES
        (N'HSI', 1, N'FLOOD == "YES"', 1.50),
        (N'HSI', 2, N'NUM_OF_CLAIM > 1 ', 1.25);
END
GO

IF NOT EXISTS (SELECT 1 FROM Product WHERE Code = N'FAI')
BEGIN
    INSERT INTO Product (Code, Name, Image, Description, MaxNumberOfInsured, Icon, Status)
    VALUES (N'FAI', N'Happy farm', N'/static/farm.jpg', N'Farm insurance', 1, N'apple', N'Active');
    INSERT INTO ProductCover (ProductCode, Code, Name, Description, Optional, SumInsured, SortOrder) VALUES
        (N'FAI', N'C1', N'Crops', N'', 0, 200000, 0),
        (N'FAI', N'C2', N'Flood', N'', 0, 100000, 1),
        (N'FAI', N'C3', N'Fire', N'', 0, 50000, 2),
        (N'FAI', N'C4', N'Equipment', N'', 1, 300000, 3);
    INSERT INTO ProductQuestion (ProductCode, Code, Kind, QuestionIndex, Text) VALUES
        (N'FAI', N'TYP', N'choice', 1, N'Cultivation type'),
        (N'FAI', N'AREA', N'numeric', 2, N'Area'),
        (N'FAI', N'NUM_OF_CLAIM', N'numeric', 3, N'Number of claims in last 5 years');
    INSERT INTO ProductChoice (ProductCode, QuestionCode, Code, Label) VALUES
        (N'FAI', N'TYP', N'ZB', N'Crop'),
        (N'FAI', N'TYP', N'KW', N'Vegetable');
    INSERT INTO TariffBaseRule (ProductCode, SortOrder, CoverCode, ApplyIfFormula, BasePriceFormula) VALUES
        (N'FAI', 1, N'C1', NULL, N'10M'),
        (N'FAI', 2, N'C2', NULL, N'20M'),
        (N'FAI', 3, N'C3', NULL, N'30M'),
        (N'FAI', 4, N'C4', NULL, N'40M');
    INSERT INTO TariffMarkupRule (ProductCode, SortOrder, ApplyIfFormula, ParamValue) VALUES
        (N'FAI', 1, N'NUM_OF_CLAIM > 2', 2.00);
END
GO

IF NOT EXISTS (SELECT 1 FROM Product WHERE Code = N'CAR')
BEGIN
    INSERT INTO Product (Code, Name, Image, Description, MaxNumberOfInsured, Icon, Status)
    VALUES (N'CAR', N'Happy Driver', N'/static/car.jpg', N'Car insurance', 1, N'car', N'Active');
    INSERT INTO ProductCover (ProductCode, Code, Name, Description, Optional, SumInsured, SortOrder) VALUES
        (N'CAR', N'C1', N'Assistance', N'', 1, NULL, 0);
    INSERT INTO ProductQuestion (ProductCode, Code, Kind, QuestionIndex, Text) VALUES
        (N'CAR', N'NUM_OF_CLAIM', N'numeric', 3, N'Number of claims in last 5 years');
    INSERT INTO TariffBaseRule (ProductCode, SortOrder, CoverCode, ApplyIfFormula, BasePriceFormula) VALUES
        (N'CAR', 1, N'C1', NULL, N'100M');
    INSERT INTO TariffMarkupRule (ProductCode, SortOrder, ApplyIfFormula, ParamValue) VALUES
        (N'CAR', 1, N'NUM_OF_CLAIM > 2', 1.50);
END
GO

IF NOT EXISTS (SELECT 1 FROM Agent WHERE Login = N'jimmy.solid')
    INSERT INTO Agent (Login, Password, Avatar, Products)
    VALUES (N'jimmy.solid', N'secret', N'static/avatars/jimmy_solid.png', N'TRI;HSI;FAI;CAR');
GO

IF NOT EXISTS (SELECT 1 FROM Agent WHERE Login = N'danny.solid')
    INSERT INTO Agent (Login, Password, Avatar, Products)
    VALUES (N'danny.solid', N'secret', N'static/avatars/danny.solid.png', N'TRI;HSI;FAI;CAR');
GO

IF NOT EXISTS (SELECT 1 FROM Agent WHERE Login = N'admin')
    INSERT INTO Agent (Login, Password, Avatar, Products)
    VALUES (N'admin', N'admin', N'static/avatars/admin.png', N'TRI;HSI;FAI;CAR');
GO

IF NOT EXISTS (SELECT 1 FROM PolicyAccount WHERE PolicyNumber = N'POLICY_1')
    INSERT INTO PolicyAccount (PolicyNumber, AccountNumber, FirstName, LastName, Status)
    VALUES (N'POLICY_1', N'231232132131', N'Tim', N'Jones', N'Active');
GO

IF NOT EXISTS (SELECT 1 FROM PolicyAccount WHERE PolicyNumber = N'POLICY_2')
    INSERT INTO PolicyAccount (PolicyNumber, AccountNumber, FirstName, LastName, Status)
    VALUES (N'POLICY_2', N'389hfswjfrh2032r', N'Mike', N'Zorn', N'Active');
GO

IF NOT EXISTS (SELECT 1 FROM PolicyAccount WHERE PolicyNumber = N'POLICY_3')
    INSERT INTO PolicyAccount (PolicyNumber, AccountNumber, FirstName, LastName, Status)
    VALUES (N'POLICY_3', N'0rju130fhj20', N'Judith', N'Powell', N'Active');
GO

IF NOT EXISTS (SELECT 1 FROM PolicySearch WHERE PolicyNumber = N'POL-SEARCH-1')
    INSERT INTO PolicySearch (PolicyNumber, PolicyHolder, ProductCode, Status)
    VALUES (N'POL-SEARCH-1', N'Tim Jones', N'TRI', N'Active');
GO

IF NOT EXISTS (SELECT 1 FROM SalesFact WHERE PolicyNumber = N'SEED-J-TRI-1')
    INSERT INTO SalesFact (PolicyNumber, AgentLogin, ProductCode, SalesDate, Premium)
    VALUES (N'SEED-J-TRI-1', N'jimmy.solid', N'TRI', '2026-01-15', 100);
GO

IF NOT EXISTS (SELECT 1 FROM SalesFact WHERE PolicyNumber = N'SEED-J-TRI-2')
    INSERT INTO SalesFact (PolicyNumber, AgentLogin, ProductCode, SalesDate, Premium)
    VALUES (N'SEED-J-TRI-2', N'jimmy.solid', N'TRI', '2026-01-20', 100);
GO

IF NOT EXISTS (SELECT 1 FROM SalesFact WHERE PolicyNumber = N'SEED-D-HSI-1')
    INSERT INTO SalesFact (PolicyNumber, AgentLogin, ProductCode, SalesDate, Premium)
    VALUES (N'SEED-D-HSI-1', N'danny.solid', N'HSI', '2026-02-01', 100);
GO

IF NOT EXISTS (SELECT 1 FROM SalesFact WHERE PolicyNumber = N'SEED-A-CAR-1')
    INSERT INTO SalesFact (PolicyNumber, AgentLogin, ProductCode, SalesDate, Premium)
    VALUES (N'SEED-A-CAR-1', N'admin', N'CAR', '2026-03-01', 250);
GO
