package com.ssafy.server.domain.room.service;

import com.ssafy.server.common.redis.RedisService;
import com.ssafy.server.domain.room.dto.request.RoomCreateRequest;
import com.ssafy.server.domain.room.dto.response.RoomDeleteResponse;
import com.ssafy.server.domain.room.dto.request.RoomPatchRequest;
import com.ssafy.server.domain.room.dto.response.RoomResponse;
import com.ssafy.server.domain.room.exception.*;
import com.ssafy.server.domain.room.types.RoomStatus;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import tools.jackson.core.JacksonException;
import tools.jackson.databind.ObjectMapper;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Set;

@Service
@RequiredArgsConstructor
public class RoomServiceImpl implements RoomService {
    private static final String ROOM_KEY_PREFIX = "room:";
    private static final String ROOM_INDEX_KEY = "rooms:all";

    private final RedisService redisService;
    private final ObjectMapper objectMapper;

    @Override
    public List<RoomResponse> getRooms() {
        Set<String> sessionNames = redisService.getSetMembers(ROOM_INDEX_KEY);
        List<RoomResponse> rooms = new ArrayList<>();

        for (String sessionName : sessionNames) {
            String json = redisService.getValue(buildRoomKey(sessionName));
            if (json == null || json.isBlank()) {
                continue;
            }

            rooms.add(fromJson(json));
        }

        rooms.sort(Comparator.comparing(RoomResponse::getSessionName));
        return rooms;
    }

    @Override
    public RoomResponse createRoom(RoomCreateRequest request) {
        RoomStatus.validateStatus(request.getStatus());

        if (request.getPlayerCount() > request.getMaxPlayers()) {
            throw new ExceededMaxPlayerException();
        }

        String roomKey = buildRoomKey(request.getSessionName());
        RoomResponse room = RoomResponse.from(request);
        if (!redisService.setIfAbsent(roomKey, toJson(room))) {
            throw new ExistSessionNameException(request.getSessionName());
        }
        redisService.addSetMember(ROOM_INDEX_KEY, request.getSessionName());
        return room;
    }

    @Override
    public RoomResponse patchRoom(RoomPatchRequest request) {
        String sessionName = request.getSessionName();
        String roomKey = buildRoomKey(sessionName);
        String json = redisService.getValue(roomKey);
        if (json == null || json.isBlank()) {
            throw new NoSessionNameException(sessionName);
        }

        RoomResponse current = fromJson(json);

        String status = request.getStatus() != null ?
                request.getStatus() : current.getStatus();
        Integer playerCount = request.getPlayerCount() != null ?
                request.getPlayerCount() : current.getPlayerCount();

        RoomStatus.validateStatus(status);
        if (playerCount > current.getMaxPlayers()) {
            throw new ExceededMaxPlayerException();
        }
        RoomResponse updated = RoomResponse.builder()
                .sessionName(current.getSessionName())
                .roomName(current.getRoomName())
                .hostNickname(current.getHostNickname())
                .status(status)
                .playerCount(playerCount)
                .maxPlayers(current.getMaxPlayers())
                .workerId(current.getWorkerId())
                .workerDisplayName(current.getWorkerDisplayName())
                .workerBaseUrl(current.getWorkerBaseUrl())
                .gameServerPid(current.getGameServerPid())
                .build();

        redisService.set(roomKey, toJson(updated));
        redisService.addSetMember(ROOM_INDEX_KEY, sessionName);
        return updated;
    }

    @Override
    public RoomDeleteResponse deleteRoom(String sessionName) {
        redisService.delete(buildRoomKey(sessionName));
        redisService.removeSetMember(ROOM_INDEX_KEY, sessionName);
        return RoomDeleteResponse.builder()
                .isOk(true)
                .sessionName(sessionName)
                .message("방 정보를 삭제했습니다.")
                .build();
    }

    private String buildRoomKey(String sessionName) {
        return ROOM_KEY_PREFIX + sessionName;
    }

    private String toJson(RoomResponse room) {
        try {
            return objectMapper.writeValueAsString(room);
        } catch (JacksonException e) {
            throw new JsonParsingException("Room 직렬화에 실패했습니다.", e);
        }
    }

    private RoomResponse fromJson(String json) {
        try {
            return objectMapper.readValue(json, RoomResponse.class);
        } catch (JacksonException e) {
            throw new JsonParsingException("Room 역직렬화에 실패했습니다.", e);
        }
    }
}
