package com.ssafy.server.domain.room.dto.response;

import com.ssafy.server.domain.room.dto.request.RoomCreateRequest;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RoomResponse {
    private String sessionName;
    private String roomName;
    private String hostNickname;
    private String status;
    private Integer playerCount;
    private Integer maxPlayers;
    private String workerId;
    private String workerDisplayName;
    private String workerBaseUrl;
    private Integer gameServerPid;

    public static RoomResponse from(RoomCreateRequest request) {
        return RoomResponse.builder()
                .sessionName(request.getSessionName())
                .roomName(request.getRoomName())
                .hostNickname(request.getHostNickname())
                .status(request.getStatus())
                .playerCount(request.getPlayerCount())
                .maxPlayers(request.getMaxPlayers())
                .workerId(request.getWorkerId())
                .workerDisplayName(request.getWorkerDisplayName())
                .workerBaseUrl(request.getWorkerBaseUrl())
                .gameServerPid(request.getGameServerPid())
                .build();
    }
}
