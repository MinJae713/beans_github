package com.ssafy.server.domain.room.dto.response;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RoomDeleteResponse {
    private boolean isOk;
    private String sessionName;
    private String message;
}
