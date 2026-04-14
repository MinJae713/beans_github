package cookssafy.feasibility;

import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Collectors;

/**
 * Phase 2 feasibility extension.
 * - Chapter 4: deadline-aware policies (EDF, LST) vs FIFO baseline.
 * - Chapter 5: shared prerequisite node — "shared-first" heuristic vs naive baselines.
 * Goal: show that policy space is rich enough to justify the curriculum's
 *       Checkpoint B learning beats (deadline reasoning, shared-node value).
 */
public final class CookSsafyFeasibilityPhase2 {

    private static final String WORKER_ID = "worker-1";

    public static void main(String[] args) {
        List<Scenario> scenarios = List.of(
                chapter4Scenario(),
                chapter5Scenario()
        );

        List<Policy> policies = List.of(
                new FifoPolicy(),
                new ShortestRemainingWorkPolicy(),
                new EdfPolicy(),
                new LeastSlackPolicy(),
                new SharedFirstPolicy()
        );

        Map<String, Result> resultIndex = new LinkedHashMap<>();
        System.out.println("== Cook SSAFY Feasibility Check :: Phase 2 (Ch4~5) ==");

        for (Scenario scenario : scenarios) {
            System.out.println();
            System.out.println("[" + scenario.chapterName + "] " + scenario.name);
            System.out.println("Intent: " + scenario.designIntent);
            for (Policy policy : policies) {
                Result result = new Simulator().run(scenario, policy);
                resultIndex.put(result.key(), result);
                System.out.println(result.summaryLine());
                if (System.getenv("PHASE2_DEBUG") != null) {
                    for (String log : result.logs) {
                        System.out.println("    " + log);
                    }
                    for (OrderOutcome outcome : result.outcomes) {
                        System.out.println("    outcome " + outcome.orderId
                                + " completion=" + outcome.completionTick
                                + " deadline=" + outcome.deadlineTick
                                + " onTime=" + outcome.onTime());
                    }
                }
            }
        }

        assertChapter4(resultIndex);
        assertChapter5(resultIndex);

        System.out.println();
        System.out.println("PHASE 2 FEASIBILITY CHECK PASSED");
    }

    /* ---------- Assertions ---------- */

    private static void assertChapter4(Map<String, Result> results) {
        // FIFO baseline should miss the most deadlines — exposes the gap.
        Result fifo = require(results, "Chapter 4", "FIFO");
        Result srw = require(results, "Chapter 4", "ShortestRemainingWork");
        Result edf = require(results, "Chapter 4", "EDF");
        Result lst = require(results, "Chapter 4", "LeastSlack");

        requireCondition(fifo.onTimeOrders < fifo.totalOrders,
                "Ch4: FIFO should miss at least one deadline");
        requireCondition(fifo.onTimeOrders < srw.onTimeOrders,
                "Ch4: FIFO must be strictly the worst — even SRW (deadline-blind) should beat it");
        requireCondition(edf.onTimeOrders > srw.onTimeOrders,
                "Ch4: EDF must strictly beat ShortestRemainingWork on on-time count");
        requireCondition(lst.onTimeOrders > srw.onTimeOrders,
                "Ch4: LST must strictly beat ShortestRemainingWork on on-time count");
        requireCondition(edf.onTimeOrders == edf.totalOrders,
                "Ch4: EDF should meet every deadline in the redesigned scenario");
        requireCondition(lst.onTimeOrders == lst.totalOrders,
                "Ch4: LST should meet every deadline in the redesigned scenario");
    }

    private static void assertChapter5(Map<String, Result> results) {
        // The shared-first heuristic should strictly beat BOTH naive baselines because
        // they cannot model the propagation value of a high-fan-out shared prerequisite.
        Result fifo = require(results, "Chapter 5", "FIFO");
        Result srw = require(results, "Chapter 5", "ShortestRemainingWork");
        Result shared = require(results, "Chapter 5", "SharedFirst");

        requireCondition(shared.completedOrders >= fifo.completedOrders,
                "Ch5: shared-first must complete at least as many orders as FIFO");
        requireCondition(shared.completedOrders >= srw.completedOrders,
                "Ch5: shared-first must complete at least as many orders as ShortestRemainingWork");
        requireCondition(
                shared.completedOrders > fifo.completedOrders
                        || shared.averageCompletionTick < fifo.averageCompletionTick,
                "Ch5: shared-first must strictly beat FIFO on completion count or average time");
        requireCondition(
                shared.completedOrders > srw.completedOrders
                        || shared.averageCompletionTick < srw.averageCompletionTick,
                "Ch5: shared-first must strictly beat ShortestRemainingWork on completion count or average time");
    }

    private static Result require(Map<String, Result> results, String chapterName, String policyName) {
        Result result = results.get(chapterName + "::" + policyName);
        if (result == null) {
            throw new IllegalStateException("Missing result for " + chapterName + " / " + policyName);
        }
        return result;
    }

    private static void requireCondition(boolean condition, String message) {
        if (!condition) {
            throw new IllegalStateException(message);
        }
    }

    /* ---------- Scenarios ---------- */

    /**
     * Chapter 4. The "two-deadline trap" — exposes the cost of being deadline-blind.
     *
     * O1 (heavy): 4 sequential 1t nodes, deadline 5. Has only 1 tick of slack, so it
     *             MUST be packed near-contiguously starting early.
     * O2 (medium-3): 3 sequential 1t nodes, deadline 8. Loose enough to be deferred.
     * O3 (instant): 1 single 1t node, deadline 2. EXTREMELY urgent — must go first.
     *
     * Only deadline-aware policies can satisfy both O1 (tight=5) and O3 (tight=2).
     * - FIFO interleaves by topo index, lets O3 starve AND blows O1 → 1/3 onTime.
     * - ShortestRemainingWork clears O3 (smallest) and O2 (second smallest) first,
     *   then O1's heavy chain spills past its deadline → 2/3 onTime.
     * - EDF / LST put O3 first (deadline 2), then O1 (deadline 5), then O2 → 3/3.
     *
     * This produces a strict ordering FIFO < SRW < {EDF, LST}.
     */
    private static Scenario chapter4Scenario() {
        MenuDef heavy = menu(
                "heavy",
                node("heavy-1", 1),
                node("heavy-2", 1, "heavy-1"),
                node("heavy-3", 1, "heavy-2"),
                node("heavy-4", 1, "heavy-3")
        );
        MenuDef medium = menu(
                "medium-3",
                node("med-1", 1),
                node("med-2", 1, "med-1"),
                node("med-3", 1, "med-2")
        );
        MenuDef instant = menu(
                "instant",
                node("inst-1", 1)
        );

        return new Scenario(
                "Chapter 4",
                "two-deadline trap (heavy + instant urgency)",
                8,
                Map.of(heavy.id, heavy, medium.id, medium, instant.id, instant),
                List.of(
                        order("O1", "heavy", 0, 5),
                        order("O2", "medium-3", 0, 8),
                        order("O3", "instant", 0, 2)
                ),
                "FIFO는 topo 인덱스로 인터리브하다 O1·O3 둘 다 놓치고, "
                        + "SRW는 O3/O2를 먼저 비우다 O1을 놓쳐야 함. "
                        + "EDF/LST는 O3 → O1 → O2 순으로 모두 on-time이어야 함."
        );
    }

    /**
     * Chapter 5. Shared prerequisite ("shared-broth") with high fan-out.
     *
     * Four ramen-family orders share the SAME shared-broth (3t) prerequisite.
     * One side dish (salad) is short and naive policies are tempted to clear it first.
     *
     * - SharedFirst sees that shared-broth has fan-out=4 (covers O1~O4) and runs it
     *   immediately, propagating to all four ramen orders. After 3 ticks, those four
     *   orders only need 1 tick each to finish. Salad gets sequenced last.
     * - ShortestRemainingWork is fooled into running salad first because its
     *   remainingWorkTotal (2) is smaller than each ramen order's 4. By the time SRW
     *   gets to shared-broth, the ramen orders all finish 2 ticks later than they
     *   would have under SharedFirst.
     * - FIFO follows topo/orderId and accidentally interleaves salad in the middle.
     *
     * Net: SharedFirst < SRW < FIFO on average completion time. The fan-out heuristic
     * creates a strict win that SRW cannot replicate without modeling shared nodes.
     */
    private static Scenario chapter5Scenario() {
        MenuDef udon = menu(
                "udon",
                node("shared-broth", 3),
                node("udon-finish", 1, "shared-broth")
        );
        MenuDef ramen = menu(
                "ramen",
                node("shared-broth", 3),
                node("ramen-finish", 1, "shared-broth")
        );
        MenuDef soba = menu(
                "soba",
                node("shared-broth", 3),
                node("soba-finish", 1, "shared-broth")
        );
        MenuDef jjampong = menu(
                "jjampong",
                node("shared-broth", 3),
                node("jjampong-finish", 1, "shared-broth")
        );
        MenuDef salad = menu(
                "side-salad",
                node("chop-veg", 1),
                node("plate-salad", 1, "chop-veg")
        );

        return new Scenario(
                "Chapter 5",
                "공유 준비작업 (4 ramen-family + 1 side)",
                10,
                Map.of(udon.id, udon, ramen.id, ramen, soba.id, soba,
                        jjampong.id, jjampong, salad.id, salad),
                List.of(
                        order("O1", "udon", 0, 20),
                        order("O2", "ramen", 0, 20),
                        order("O3", "soba", 0, 20),
                        order("O4", "jjampong", 0, 20),
                        order("O5", "side-salad", 0, 20)
                ),
                "shared-broth의 fan-out=4를 인식하는 SharedFirst가 SRW(샐러드를 먼저 비우다 ramen 4건이 전부 늦어짐)와 "
                        + "FIFO(샐러드를 중간에 끼워 넣어 더 늦어짐)를 평균 완료시간에서 모두 strict하게 이겨야 함"
        );
    }

    /* ---------- Policies ---------- */

    private interface Policy {
        String name();

        Assignment decide(KitchenState state);
    }

    /** FIFO: earliest arrived order, tie-broken by topological index. */
    private static final class FifoPolicy implements Policy {
        @Override public String name() { return "FIFO"; }
        @Override public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.arrivedTick)
                            .thenComparingInt(c -> c.topologicalIndex)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(c -> new Assignment(c.orderId, c.nodeId))
                    .orElse(null);
        }
    }

    /** ShortestRemainingWork: from Phase 1, used as a deadline-blind baseline. */
    private static final class ShortestRemainingWorkPolicy implements Policy {
        @Override public String name() { return "ShortestRemainingWork"; }
        @Override public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.remainingWorkTotal)
                            .thenComparingInt(c -> c.duration)
                            .thenComparingInt(c -> c.arrivedTick)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(c -> new Assignment(c.orderId, c.nodeId))
                    .orElse(null);
        }
    }

    /** EDF: earliest deadline first. */
    private static final class EdfPolicy implements Policy {
        @Override public String name() { return "EDF"; }
        @Override public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.deadlineTick)
                            .thenComparingInt(c -> c.topologicalIndex)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(c -> new Assignment(c.orderId, c.nodeId))
                    .orElse(null);
        }
    }

    /** LeastSlack: slack = deadline - now - remainingWorkTotal. */
    private static final class LeastSlackPolicy implements Policy {
        @Override public String name() { return "LeastSlack"; }
        @Override public Assignment decide(KitchenState state) {
            final int now = state.tick;
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.deadlineTick - now - c.remainingWorkTotal)
                            .thenComparingInt(c -> c.deadlineTick)
                            .thenComparingInt(c -> c.topologicalIndex)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(c -> new Assignment(c.orderId, c.nodeId))
                    .orElse(null);
        }
    }

    /**
     * SharedFirst: prefer nodes whose fan-out covers more pending orders.
     * Uses menuCatalog to count how many *other* pending orders currently block
     * on a node with the same id as the candidate.
     */
    private static final class SharedFirstPolicy implements Policy {
        @Override public String name() { return "SharedFirst"; }
        @Override public Assignment decide(KitchenState state) {
            Map<String, Integer> fanOut = new HashMap<>();
            for (OrderSnapshot order : state.pendingOrders) {
                for (String nodeId : order.availableNodeIds) {
                    fanOut.merge(nodeId, 1, Integer::sum);
                }
            }

            return state.availableAssignments.stream()
                    .min(Comparator
                            // higher fan-out first (so negate)
                            .comparingInt((AssignmentCandidate c) -> -fanOut.getOrDefault(c.nodeId, 1))
                            .thenComparingInt(c -> c.deadlineTick)
                            .thenComparingInt(c -> c.remainingWorkTotal)
                            .thenComparingInt(c -> c.arrivedTick)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(c -> new Assignment(c.orderId, c.nodeId))
                    .orElse(null);
        }
    }

    /* ---------- Simulator (copied & adapted from Phase 1) ---------- */

    private static final class Simulator {
        Result run(Scenario scenario, Policy policy) {
            Map<String, OrderRuntime> orders = new LinkedHashMap<>();
            for (OrderPlan orderPlan : scenario.orders) {
                orders.put(orderPlan.id, new OrderRuntime(orderPlan));
            }

            ActiveTask activeTask = null;
            List<String> logs = new ArrayList<>();

            // Shared-node awareness: once a shared node id is completed by one order,
            // mirror that completion to every *other* order whose menu also contains
            // a node with the same id. This models a shared prep station.
            for (int tick = 0; tick <= scenario.tickBudget; tick++) {
                final int currentTick = tick;
                if (activeTask != null && activeTask.finishTick == tick) {
                    OrderRuntime orderRuntime = orders.get(activeTask.orderId);
                    MenuDef menuDef = scenario.menus.get(orderRuntime.plan.menuId);
                    orderRuntime.complete(activeTask.nodeId, tick, menuDef);
                    logs.add("tick " + tick + ": complete " + activeTask.orderId + "/" + activeTask.nodeId);

                    // propagate to siblings sharing the same node id
                    for (OrderRuntime sibling : orders.values()) {
                        if (sibling == orderRuntime) continue;
                        MenuDef siblingMenu = scenario.menus.get(sibling.plan.menuId);
                        if (siblingMenu.nodeById.containsKey(activeTask.nodeId)
                                && !sibling.completedNodeIds.contains(activeTask.nodeId)
                                && !Objects.equals(sibling.activeNodeId, activeTask.nodeId)) {
                            sibling.forceComplete(activeTask.nodeId, siblingMenu);
                            logs.add("tick " + tick + ": share-propagate "
                                    + sibling.plan.id + "/" + activeTask.nodeId);
                        }
                    }
                    activeTask = null;
                }

                List<OrderRuntime> arrivedOrders = orders.values().stream()
                        .filter(order -> order.plan.arrivedTick <= currentTick)
                        .toList();

                if (tick == scenario.tickBudget || activeTask != null) {
                    continue;
                }

                KitchenState state = snapshot(scenario, arrivedOrders, orders.values(), tick);
                Assignment assignment = policy.decide(state);
                AssignmentCandidate candidate = validateAssignment(assignment, state.availableAssignments);

                if (candidate == null) {
                    if (assignment != null) {
                        logs.add("tick " + tick + ": invalid assignment " + assignment.orderId + "/" + assignment.nodeId);
                    }
                    continue;
                }

                OrderRuntime orderRuntime = orders.get(candidate.orderId);
                orderRuntime.start(candidate.nodeId);
                activeTask = new ActiveTask(candidate.orderId, candidate.nodeId, tick + candidate.duration);
                logs.add("tick " + tick + ": start " + candidate.orderId + "/" + candidate.nodeId + " (" + candidate.duration + "t)");
            }

            List<OrderOutcome> outcomes = orders.values().stream()
                    .map(orderRuntime -> {
                        MenuDef menuDef = scenario.menus.get(orderRuntime.plan.menuId);
                        return new OrderOutcome(
                                orderRuntime.plan.id,
                                orderRuntime.plan.menuId,
                                menuDef.totalDuration(),
                                orderRuntime.completionTick,
                                orderRuntime.plan.deadlineTick,
                                orderRuntime.completedNodeIds.size(),
                                menuDef.nodes.size()
                        );
                    })
                    .toList();

            int completedOrders = (int) outcomes.stream().filter(OrderOutcome::completed).count();
            int onTimeOrders = (int) outcomes.stream().filter(OrderOutcome::onTime).count();
            double averageCompletion = outcomes.stream()
                    .filter(OrderOutcome::completed)
                    .mapToInt(o -> o.completionTick)
                    .average()
                    .orElse(Double.NaN);

            return new Result(
                    scenario.chapterName,
                    scenario.name,
                    policy.name(),
                    scenario.tickBudget,
                    scenario.orders.size(),
                    completedOrders,
                    onTimeOrders,
                    averageCompletion,
                    outcomes,
                    logs
            );
        }

        private KitchenState snapshot(Scenario scenario,
                                      List<OrderRuntime> arrivedOrders,
                                      Collection<OrderRuntime> allOrders,
                                      int tick) {
            List<OrderSnapshot> pendingOrders = new ArrayList<>();
            List<AssignmentCandidate> candidates = new ArrayList<>();

            for (OrderRuntime orderRuntime : arrivedOrders) {
                MenuDef menuDef = scenario.menus.get(orderRuntime.plan.menuId);
                if (orderRuntime.isCompleted(menuDef)) {
                    continue;
                }

                List<String> availableNodeIds = orderRuntime.availableNodeIds(menuDef);
                OrderSnapshot orderSnapshot = new OrderSnapshot(
                        orderRuntime.plan.id,
                        orderRuntime.plan.menuId,
                        orderRuntime.plan.arrivedTick,
                        orderRuntime.plan.deadlineTick,
                        List.copyOf(orderRuntime.completedNodeIds),
                        availableNodeIds,
                        orderRuntime.remainingWorkTotal(menuDef)
                );
                pendingOrders.add(orderSnapshot);

                for (String nodeId : availableNodeIds) {
                    RecipeNode recipeNode = menuDef.nodeById.get(nodeId);
                    candidates.add(new AssignmentCandidate(
                            WORKER_ID,
                            orderRuntime.plan.id,
                            nodeId,
                            recipeNode.duration,
                            orderRuntime.plan.arrivedTick,
                            orderRuntime.plan.deadlineTick,
                            orderRuntime.remainingWorkTotal(menuDef),
                            menuDef.indexOf(nodeId)
                    ));
                }
            }

            // Deduplicate candidates by (orderId, nodeId) in a stable order,
            // but do NOT collapse duplicates across orders — shared node id across
            // different orders still produces distinct candidates (one per order).
            candidates.sort(Comparator
                    .comparingInt((AssignmentCandidate c) -> c.arrivedTick)
                    .thenComparingInt(c -> c.topologicalIndex)
                    .thenComparing(c -> c.orderId)
                    .thenComparing(c -> c.nodeId));

            return new KitchenState(
                    tick,
                    scenario.tickBudget,
                    pendingOrders,
                    candidates,
                    scenario.menus
            );
        }

        private AssignmentCandidate validateAssignment(Assignment assignment, List<AssignmentCandidate> candidates) {
            if (assignment == null) return null;
            return candidates.stream()
                    .filter(c -> c.orderId.equals(assignment.orderId) && c.nodeId.equals(assignment.nodeId))
                    .findFirst()
                    .orElse(null);
        }
    }

    /* ---------- Data classes ---------- */

    private static OrderPlan order(String id, String menuId, int arrivedTick, int deadlineTick) {
        return new OrderPlan(id, menuId, arrivedTick, deadlineTick);
    }

    private static RecipeNode node(String id, int duration, String... prerequisites) {
        return new RecipeNode(id, duration, List.of(prerequisites));
    }

    private static MenuDef menu(String id, RecipeNode... nodes) {
        return new MenuDef(id, List.of(nodes));
    }

    private static final class Scenario {
        private final String chapterName;
        private final String name;
        private final int tickBudget;
        private final Map<String, MenuDef> menus;
        private final List<OrderPlan> orders;
        private final String designIntent;

        private Scenario(String chapterName, String name, int tickBudget, Map<String, MenuDef> menus,
                         List<OrderPlan> orders, String designIntent) {
            this.chapterName = chapterName;
            this.name = name;
            this.tickBudget = tickBudget;
            this.menus = menus;
            this.orders = orders;
            this.designIntent = designIntent;
        }
    }

    private static final class MenuDef {
        private final String id;
        private final List<RecipeNode> nodes;
        private final Map<String, RecipeNode> nodeById;
        private final Map<String, Integer> indexByNodeId;

        private MenuDef(String id, List<RecipeNode> nodes) {
            this.id = id;
            this.nodes = List.copyOf(nodes);
            this.nodeById = new HashMap<>();
            this.indexByNodeId = new HashMap<>();
            for (int i = 0; i < nodes.size(); i++) {
                RecipeNode node = nodes.get(i);
                this.nodeById.put(node.id, node);
                this.indexByNodeId.put(node.id, i);
            }
        }

        private int totalDuration() {
            return nodes.stream().mapToInt(node -> node.duration).sum();
        }

        private int indexOf(String nodeId) {
            return indexByNodeId.get(nodeId);
        }
    }

    private static final class RecipeNode {
        private final String id;
        private final int duration;
        private final List<String> prerequisites;

        private RecipeNode(String id, int duration, List<String> prerequisites) {
            this.id = id;
            this.duration = duration;
            this.prerequisites = List.copyOf(prerequisites);
        }
    }

    private static final class OrderPlan {
        private final String id;
        private final String menuId;
        private final int arrivedTick;
        private final int deadlineTick;

        private OrderPlan(String id, String menuId, int arrivedTick, int deadlineTick) {
            this.id = id;
            this.menuId = menuId;
            this.arrivedTick = arrivedTick;
            this.deadlineTick = deadlineTick;
        }
    }

    private static final class OrderRuntime {
        private final OrderPlan plan;
        private final Set<String> completedNodeIds = new LinkedHashSet<>();
        private String activeNodeId;
        private Integer completionTick;

        private OrderRuntime(OrderPlan plan) {
            this.plan = plan;
        }

        private void start(String nodeId) {
            this.activeNodeId = nodeId;
        }

        private void complete(String nodeId, int tick, MenuDef menuDef) {
            if (!Objects.equals(activeNodeId, nodeId)) {
                throw new IllegalStateException("Attempted to complete a node that is not active: " + nodeId);
            }
            activeNodeId = null;
            completedNodeIds.add(nodeId);
            if (completedNodeIds.size() == menuDef.nodes.size()) {
                completionTick = tick;
            }
        }

        /** Force-mark a node as completed because a sibling already finished the shared work. */
        private void forceComplete(String nodeId, MenuDef menuDef) {
            completedNodeIds.add(nodeId);
            if (completedNodeIds.size() == menuDef.nodes.size() && completionTick == null) {
                completionTick = -1; // completed via propagation; will be overwritten if later activity updates it
            }
        }

        private boolean isCompleted(MenuDef menuDef) {
            return completedNodeIds.size() == menuDef.nodes.size();
        }

        private int remainingWorkTotal(MenuDef menuDef) {
            return menuDef.nodes.stream()
                    .filter(node -> !completedNodeIds.contains(node.id))
                    .filter(node -> !Objects.equals(activeNodeId, node.id))
                    .mapToInt(node -> node.duration)
                    .sum();
        }

        private List<String> availableNodeIds(MenuDef menuDef) {
            return menuDef.nodes.stream()
                    .filter(node -> !completedNodeIds.contains(node.id))
                    .filter(node -> !Objects.equals(activeNodeId, node.id))
                    .filter(node -> completedNodeIds.containsAll(node.prerequisites))
                    .map(node -> node.id)
                    .toList();
        }
    }

    private record ActiveTask(String orderId, String nodeId, int finishTick) {
    }

    private record KitchenState(
            int tick,
            int tickBudget,
            List<OrderSnapshot> pendingOrders,
            List<AssignmentCandidate> availableAssignments,
            Map<String, MenuDef> menuCatalog
    ) {
    }

    private record OrderSnapshot(
            String orderId,
            String menuId,
            int arrivedTick,
            int deadlineTick,
            List<String> completedNodeIds,
            List<String> availableNodeIds,
            int remainingWorkTotal
    ) {
    }

    private record AssignmentCandidate(
            String employeeId,
            String orderId,
            String nodeId,
            int duration,
            int arrivedTick,
            int deadlineTick,
            int remainingWorkTotal,
            int topologicalIndex
    ) {
    }

    private record Assignment(String orderId, String nodeId) {
    }

    private record OrderOutcome(
            String orderId,
            String menuId,
            int totalWork,
            Integer completionTick,
            int deadlineTick,
            int completedNodeCount,
            int totalNodeCount
    ) {
        private boolean completed() { return completionTick != null; }
        private boolean onTime() { return completionTick != null && completionTick <= deadlineTick; }
    }

    private static final class Result {
        private final String chapterName;
        private final String scenarioName;
        private final String policyName;
        private final int tickBudget;
        private final int totalOrders;
        private final int completedOrders;
        private final int onTimeOrders;
        private final double averageCompletionTick;
        private final List<OrderOutcome> outcomes;
        private final List<String> logs;

        private Result(String chapterName, String scenarioName, String policyName, int tickBudget,
                       int totalOrders, int completedOrders, int onTimeOrders, double averageCompletionTick,
                       List<OrderOutcome> outcomes, List<String> logs) {
            this.chapterName = chapterName;
            this.scenarioName = scenarioName;
            this.policyName = policyName;
            this.tickBudget = tickBudget;
            this.totalOrders = totalOrders;
            this.completedOrders = completedOrders;
            this.onTimeOrders = onTimeOrders;
            this.averageCompletionTick = averageCompletionTick;
            this.outcomes = outcomes;
            this.logs = logs;
        }

        private String key() { return chapterName + "::" + policyName; }

        private String summaryLine() {
            String avg = Double.isNaN(averageCompletionTick)
                    ? "-"
                    : String.format(Locale.US, "%.2f", averageCompletionTick);
            String completedOrderIds = outcomes.stream()
                    .filter(OrderOutcome::completed)
                    .map(OrderOutcome::orderId)
                    .collect(Collectors.joining(","));
            if (completedOrderIds.isBlank()) completedOrderIds = "-";

            return String.format(Locale.US,
                    "  %-22s completed=%d/%d onTime=%d/%d avgCompletion=%s orders=%s",
                    policyName, completedOrders, totalOrders, onTimeOrders, totalOrders, avg, completedOrderIds);
        }
    }
}
