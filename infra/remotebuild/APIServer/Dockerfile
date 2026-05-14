FROM eclipse-temurin:21-jre

WORKDIR /app

# Safe defaults for direct container execution.
# Real production values must be injected by compose/.env/EC2 environment.
ENV REDIS_HOST=redis
ENV REDIS_PORT=6379
ENV REDIS_SSL_ENABLED=true
ENV APP_CORS_ALLOWED_ORIGINS=https://prod-domain.example,https://dev-domain.example

COPY server-0.0.1-SNAPSHOT.jar app.jar

EXPOSE 8081

ENTRYPOINT ["java", "-jar", "/app/app.jar"]
