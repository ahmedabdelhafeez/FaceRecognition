FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Create onnx directory and copy models
RUN mkdir -p /app/onnx
COPY onnx/ /app/onnx/

# Create data directory for SQLite database
RUN mkdir -p /data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5050
ENV FaceRecognition__OnnxDirectory=/app/onnx
ENV ConnectionStrings__FaceDb="Server=db46864.public.databaseasp.net; Database=db46864; User Id=db46864; Password=Sk6@%9MjZ?m3; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"

EXPOSE 8080

ENTRYPOINT ["dotnet", "FaceRecognition.dll"]
