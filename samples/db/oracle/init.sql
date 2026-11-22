-- Oracle schema for the Loki samples (the SQL Server schema is the WebApi's EF Core migrations).
--
-- The gvenzl/oracle-free image runs this file once, on the container's first start, with `sqlplus / as sysdba` in the
-- container database root, after it created APP_USER (loki_samples, see docker-compose.yml). So the script switches to the
-- FREEPDB1 pluggable database and creates every object in the LOKI_SAMPLES schema. Run it by hand (as SYSDBA) against another
-- database only after changing those two names.
--
-- Unquoted names: Oracle folds them to upper case, as Loki's default OracleIdentifierResolution.Unquoted does, so the samples'
-- "Customers" and "STG_PRODUCTS" resolve to CUSTOMERS and STG_PRODUCTS.

WHENEVER SQLERROR EXIT SQL.SQLCODE

ALTER SESSION SET CONTAINER = FREEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = LOKI_SAMPLES;

CREATE TABLE Customers (
    Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_Customers PRIMARY KEY,
    Name VARCHAR2(200) NOT NULL,
    Email VARCHAR2(320),
    CreatedOn TIMESTAMP(7) NOT NULL);

CREATE TABLE Orders (
    Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId NUMBER(10) NOT NULL CONSTRAINT FK_Orders_Customers_CustomerId REFERENCES Customers (Id) ON DELETE CASCADE,
    Reference VARCHAR2(50) NOT NULL,
    Amount NUMBER(18, 2) NOT NULL,
    OrderedOn TIMESTAMP(7) NOT NULL);

CREATE INDEX IX_Orders_CustomerId ON Orders (CustomerId);

CREATE TABLE Products (
    Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_Products PRIMARY KEY,
    Sku VARCHAR2(50) NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    Name VARCHAR2(200) NOT NULL,
    Price NUMBER(18, 2) NOT NULL);

-- Staging table for the direct-path import (OracleWriteMode.DirectPath, registered as "Oracle.DirectPath"). It is built to
-- avoid the direct-path hazards in the README: no primary or unique key and no index (duplicate keys cannot make an index
-- UNUSABLE, and there is no index to be UNUSABLE), no foreign key (none to skip), no trigger (ORA-26086), and no other
-- application reads it. Constraints are enforced when the rows move on to Products with a conventional INSERT ... SELECT.
-- BatchId keeps concurrent imports apart.
CREATE TABLE STG_PRODUCTS (
    BatchId VARCHAR2(32) NOT NULL,
    Sku VARCHAR2(50),
    Name VARCHAR2(200),
    Price NUMBER(18, 2));

-- Used only by Loki.Samples.Benchmark (EF Core and Loki insert into it; the benchmark empties it before every run).
CREATE TABLE BenchmarkRows (
    Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_BenchmarkRows PRIMARY KEY,
    Name VARCHAR2(100) NOT NULL,
    Amount NUMBER(18, 2) NOT NULL,
    Quantity NUMBER(10) NOT NULL,
    CreatedOn TIMESTAMP(7) NOT NULL);
