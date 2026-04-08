FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

# ✅ Restore WITHOUT runtime identifier so all native assets are included
RUN dotnet restore

RUN dotnet publish -c Release \
    --no-restore \
    -o /app/publish

# Debug: confirm onnxruntime.so is in publish output
RUN find /app/publish -name "*onnx*" && echo "✅ ONNXRuntime found" || echo "❌ ONNXRuntime MISSING"

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

RUN apt-get update && apt-get install -y \
    libgomp1 \
    libstdc++6 \
    libc6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/onnx
COPY onnx/ /app/onnx/

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5050
ENV FaceRecognition__OnnxDirectory=/app/onnx
ENV LD_LIBRARY_PATH=/app:$LD_LIBRARY_PATH
ENV ConnectionStrings__FaceDb="Server=db46864.public.databaseasp.net,1433;Database=db46864;User Id=db46864;Password=Sk6@%9MjZ?m3;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Pooling=False;"

EXPOSE 5050

ENTRYPOINT ["dotnet", "FaceRecognition.dll"]