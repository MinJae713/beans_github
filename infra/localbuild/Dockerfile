FROM eclipse-temurin:21-jre

WORKDIR /app

# Default values for direct local container execution.
# When started via docker-compose, REDIS_HOST is overridden to the Redis service name.
ENV REDIS_HOST=localhost
ENV REDIS_PORT=6379
ENV REDIS_SSL_ENABLED=false

COPY server-0.0.1-SNAPSHOT.jar app.jar

EXPOSE 8081

ENTRYPOINT ["java", "-jar", "/app/app.jar"]
