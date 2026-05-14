package com.ssafy.server.common.redis;

import lombok.RequiredArgsConstructor;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;

import java.util.Collections;
import java.util.Set;
import java.util.concurrent.TimeUnit;

@Component
@RequiredArgsConstructor
public class RedisService {
    private final StringRedisTemplate stringRedisTemplate;

    public String getValue(String key) {
        return stringRedisTemplate.opsForValue().get(key);
    }

    public void set(String key, String value) {
        stringRedisTemplate.opsForValue().set(key, value);
    }

    public boolean setIfAbsent(String key, String value) {
        Boolean created = stringRedisTemplate.opsForValue().setIfAbsent(key, value);
        return Boolean.TRUE.equals(created);
    }

    public void set(String key, String value, Long ttl) {
        stringRedisTemplate.opsForValue().set(key, value, ttl, TimeUnit.SECONDS);
    }

    public boolean delete(String key) {
        Boolean deleted = stringRedisTemplate.delete(key);
        return Boolean.TRUE.equals(deleted);
    }

    public boolean hasKey(String key) {
        Boolean exists = stringRedisTemplate.hasKey(key);
        return Boolean.TRUE.equals(exists);
    }

    public Set<String> getSetMembers(String key) {
        Set<String> members = stringRedisTemplate.opsForSet().members(key);
        return members != null ? members : Collections.emptySet();
    }

    public void addSetMember(String key, String member) {
        stringRedisTemplate.opsForSet().add(key, member);
    }

    public void removeSetMember(String key, String member) {
        stringRedisTemplate.opsForSet().remove(key, member);
    }
}
