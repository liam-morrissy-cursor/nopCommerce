# musl cdylib for the Alpine runtime. Pinned to amd64 so the triple stays x86_64-unknown-linux-musl.
FROM --platform=linux/amd64 rust:1-alpine AS price-rounding
RUN apk add --no-cache musl-dev
WORKDIR /native/price-rounding
COPY native/price-rounding/Cargo.toml native/price-rounding/Cargo.lock ./
COPY native/price-rounding/src ./src
RUN cargo build --release --target x86_64-unknown-linux-musl

# create the build instance 
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

ARG TARGETPLATFORM
ARG BUILDPLATFORM

WORKDIR /src                                                                    
COPY ./src ./

# build solution   
RUN dotnet build NopCommerce.sln --no-incremental -c Release

# publish project
WORKDIR /src/Presentation/Nop.Web   
RUN dotnet publish Nop.Web.csproj -c Release -o /app/published

WORKDIR /app/published

RUN mkdir logs bin

RUN chmod 775 App_Data \
              App_Data/DataProtectionKeys \
              bin \
              logs \
              Plugins \
              wwwroot/bundles \
              wwwroot/db_backups \
              wwwroot/files/exportimport \
              wwwroot/icons \
              wwwroot/images \
              wwwroot/images/thumbs \
              wwwroot/images/uploaded \
	      wwwroot/sitemaps

# create the runtime instance 
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime 

# add globalization support
RUN apk add --no-cache icu-libs icu-data-full
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# installs required packages
RUN apk add tiff --no-cache --repository https://dl-cdn.alpinelinux.org/alpine/edge/main/ --allow-untrusted
RUN apk add libgdiplus --no-cache --repository https://dl-cdn.alpinelinux.org/alpine/edge/community/ --allow-untrusted
RUN apk add libc-dev tzdata gcompat --no-cache

WORKDIR /app

COPY --from=build /app/published .
# Alpine is musl. The price-rounding stage builds x86_64-unknown-linux-musl; a glibc .so will not load here.
COPY --from=price-rounding /native/price-rounding/target/x86_64-unknown-linux-musl/release/libprice_rounding.so ./libprice_rounding.so

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
                            
ENTRYPOINT ["dotnet", "Nop.Web.dll"]