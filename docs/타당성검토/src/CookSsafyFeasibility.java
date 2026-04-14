package cookssafy.feasibility;

import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Collectors;

public final class CookSsafyFeasibility {

    private static final String WORKER_ID = "worker-1";

    public static void main(String[] args) {
        List<Scenario> scenarios = List.of(
                chapter1Scenario(),
                chapter2Scenario(),
                chapter3Scenario()
        );

        List<Policy> policies = List.of(
                new FirstAvailablePolicy(),
                new FifoPolicy(),
                new ShortestCurrentDurationPolicy(),
                new ShortestRemainingWorkPolicy()
        );

        Map<String, Result> resultIndex = new LinkedHashMap<>();
        System.out.println("== Cook SSAFY Feasibility Check ==");

        for (Scenario scenario : scenarios) {
            System.out.println();
            System.out.println("[" + scenario.chapterName + "] " + scenario.name);
            System.out.println("Intent: " + scenario.designIntent);
            for (Policy policy : policies) {
                Result result = new Simulator().run(scenario, policy);
                resultIndex.put(result.key(), result);
                System.out.println(result.summaryLine());
            }
        }

        assertChapter1(resultIndex);
        assertChapter2(resultIndex);
        assertChapter3(resultIndex);

        System.out.println();
        System.out.println("FEASIBILITY CHECK PASSED");
    }

    private static void assertChapter1(Map<String, Result> results) {
        for (String policyName : List.of("FirstAvailable", "FIFO", "ShortestCurrentDuration", "ShortestRemainingWork")) {
            Result result = require(results, "Chapter 1", policyName);
            requireCondition(result.completedOrders == 1,
                    "Chapter 1 should be solvable by " + policyName);
            requireCondition(Math.abs(result.averageCompletionTick - 3.0) < 0.0001,
                    "Chapter 1 completion tick should remain stable for " + policyName);
        }
    }

    private static void assertChapter2(Map<String, Result> results) {
        Result fifo = require(results, "Chapter 2", "FIFO");
        Result shortestRemaining = require(results, "Chapter 2", "ShortestRemainingWork");

        requireCondition(fifo.completedOrders == 2,
                "Chapter 2 FIFO baseline should complete exactly two orders inside the budget");
        requireCondition(shortestRemaining.completedOrders == 2,
                "Chapter 2 shortest-remaining strategy should also complete exactly two orders");
        requireCondition(shortestRemaining.averageCompletionTick < fifo.averageCompletionTick,
                "Chapter 2 should reward remaining-work-aware prioritization with a better average completion time");
    }

    private static void assertChapter3(Map<String, Result> results) {
        Result fifo = require(results, "Chapter 3", "FIFO");
        Result shortestCurrent = require(results, "Chapter 3", "ShortestCurrentDuration");
        Result shortestRemaining = require(results, "Chapter 3", "ShortestRemainingWork");

        requireCondition(fifo.completedOrders == 0,
                "Chapter 3 FIFO baseline should fail to complete any order inside the budget");
        requireCondition(shortestCurrent.completedOrders == 2,
                "Chapter 3 ShortestCurrentDuration should complete exactly two orders");
        requireCondition(shortestRemaining.completedOrders == 2,
                "Chapter 3 ShortestRemainingWork should complete exactly two orders");
        requireCondition(shortestCurrent.completedOrders > fifo.completedOrders,
                "Chapter 3 should show a strict completion-count gap between FIFO and duration-aware heuristics");
        requireCondition(shortestRemaining.averageCompletionTick < shortestCurrent.averageCompletionTick,
                "Chapter 3 ShortestRemainingWork should achieve a strictly lower averageCompletionTick than ShortestCurrentDuration");
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

    private static Scenario chapter1Scenario() {
        MenuDef toast = menu(
                "toast",
                node("prep-bread", 1),
                node("toast-bread", 1, "prep-bread"),
                node("plate-toast", 1, "toast-bread")
        );

        return new Scenario(
                "Chapter 1",
                "단일 주문 선형 DAG",
                10,
                Map.of(toast.id, toast),
                List.of(order("O1", "toast", 0, 10)),
                "최소 정책으로도 단일 DAG를 끝까지 완주할 수 있는지 확인"
        );
    }

    private static Scenario chapter2Scenario() {
        MenuDef combo = menu(
                "combo",
                node("combo-1", 1),
                node("combo-2", 1, "combo-1"),
                node("combo-3", 1, "combo-2"),
                node("combo-4", 1, "combo-3")
        );
        MenuDef salad = menu(
                "salad",
                node("salad-1", 1),
                node("salad-2", 1, "salad-1")
        );
        MenuDef soup = menu(
                "soup",
                node("soup-1", 1),
                node("soup-2", 1, "soup-1")
        );

        return new Scenario(
                "Chapter 2",
                "여러 주문과 남은 작업량 비교",
                6,
                Map.of(combo.id, combo, salad.id, salad, soup.id, soup),
                List.of(
                        order("O1", "combo", 0, 20),
                        order("O2", "salad", 0, 20),
                        order("O3", "soup", 1, 20)
                ),
                "FIFO와 남은 총 작업량 기준 정책이 평균 완료시간에서 실제 차이를 만드는지 확인"
        );
    }

    private static Scenario chapter3Scenario() {
        // O1 noodle: 3-node chain — boil(1) → simmer(3) → plate(1), total=5
        // O2 side:   2-node chain — chop(3) → toss(1),              total=4
        // O3 drink:  2-node chain — pour(1) → garnish(1),           total=2, arrives tick 2
        //
        // At tick 0 only O1 and O2 are present:
        //   candidate boil (dur=1, rem=4*)  vs chop (dur=3, rem=1*)
        //   *rem = remaining AFTER the current node, but here rem is computed
        //    as uncompleted+non-active nodes (includes the node itself if not yet started):
        //   boil(O1): dur=1, remainingWorkTotal = simmer(3)+plate(1) = 4  ... wait no:
        //   remainingWorkTotal excludes the active node. Before any node starts:
        //     boil is not active yet → included? No: availableNodeIds filters
        //     out activeNodeId and completedNodeIds; remainingWorkTotal also
        //     excludes active. At tick 0 nothing is active or completed yet, so:
        //     O1 remainingWorkTotal = boil(1)+simmer(3)+plate(1) = 5
        //     O2 remainingWorkTotal = chop(3)+toss(1) = 4
        //
        //   SCD picks by (duration ASC, remainingWorkTotal ASC):
        //     boil(dur=1, rem=5) beats chop(dur=3, rem=4) → picks boil(O1)
        //   SRW picks by (remainingWorkTotal ASC, duration ASC):
        //     chop(rem=4, dur=3) beats boil(rem=5, dur=1) → picks chop(O2)
        //
        // This divergence at tick 0 causes different completion orderings:
        //   SCD finishes O1(tick 5) and O3(tick 7)  → completed=2, avg=6.00
        //   SRW finishes O2(tick 4) and O3(tick 6)  → completed=2, avg=5.00
        //   SRW strictly beats SCD on averageCompletionTick.
        MenuDef noodle = menu(
                "noodle",
                node("boil", 1),
                node("simmer", 3, "boil"),
                node("plate-noodle", 1, "simmer")
        );
        MenuDef side = menu(
                "side",
                node("chop", 3),
                node("toss", 1, "chop")
        );
        MenuDef drink = menu(
                "drink",
                node("pour", 1),
                node("garnish", 1, "pour")
        );

        return new Scenario(
                "Chapter 3",
                "긴 작업과 짧은 작업 혼합",
                7,
                Map.of(noodle.id, noodle, side.id, side, drink.id, drink),
                List.of(
                        order("O1", "noodle", 0, 20),
                        order("O2", "side",   0, 20),
                        order("O3", "drink",  2, 20)
                ),
                "duration-aware 그리디와 남은-총작업량 그리디가 FIFO보다 우수하되, 두 정책 사이에서도 평균완료시간 차이가 발생하는지 확인"
        );
    }

    private static OrderPlan order(String id, String menuId, int arrivedTick, int deadlineTick) {
        return new OrderPlan(id, menuId, arrivedTick, deadlineTick);
    }

    private static RecipeNode node(String id, int duration, String... prerequisites) {
        return new RecipeNode(id, duration, List.of(prerequisites));
    }

    private static MenuDef menu(String id, RecipeNode... nodes) {
        return new MenuDef(id, List.of(nodes));
    }

    private interface Policy {
        String name();

        Assignment decide(KitchenState state);
    }

    private static final class FirstAvailablePolicy implements Policy {
        @Override
        public String name() {
            return "FirstAvailable";
        }

        @Override
        public Assignment decide(KitchenState state) {
            if (state.availableAssignments.isEmpty()) {
                return null;
            }
            AssignmentCandidate candidate = state.availableAssignments.get(0);
            return new Assignment(candidate.orderId, candidate.nodeId);
        }
    }

    private static final class FifoPolicy implements Policy {
        @Override
        public String name() {
            return "FIFO";
        }

        @Override
        public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.arrivedTick)
                            .thenComparingInt(c -> c.topologicalIndex)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(candidate -> new Assignment(candidate.orderId, candidate.nodeId))
                    .orElse(null);
        }
    }

    private static final class ShortestCurrentDurationPolicy implements Policy {
        @Override
        public String name() {
            return "ShortestCurrentDuration";
        }

        @Override
        public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.duration)
                            .thenComparingInt(c -> c.remainingWorkTotal)
                            .thenComparingInt(c -> c.arrivedTick)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(candidate -> new Assignment(candidate.orderId, candidate.nodeId))
                    .orElse(null);
        }
    }

    private static final class ShortestRemainingWorkPolicy implements Policy {
        @Override
        public String name() {
            return "ShortestRemainingWork";
        }

        @Override
        public Assignment decide(KitchenState state) {
            return state.availableAssignments.stream()
                    .min(Comparator
                            .comparingInt((AssignmentCandidate c) -> c.remainingWorkTotal)
                            .thenComparingInt(c -> c.duration)
                            .thenComparingInt(c -> c.arrivedTick)
                            .thenComparing(c -> c.orderId)
                            .thenComparing(c -> c.nodeId))
                    .map(candidate -> new Assignment(candidate.orderId, candidate.nodeId))
                    .orElse(null);
        }
    }

    private static final class Simulator {
        Result run(Scenario scenario, Policy policy) {
            Map<String, OrderRuntime> orders = new LinkedHashMap<>();
            for (OrderPlan orderPlan : scenario.orders) {
                orders.put(orderPlan.id, new OrderRuntime(orderPlan));
            }

            ActiveTask activeTask = null;
            List<String> logs = new ArrayList<>();

            for (int tick = 0; tick <= scenario.tickBudget; tick++) {
                final int currentTick = tick;
                if (activeTask != null && activeTask.finishTick == tick) {
                    OrderRuntime orderRuntime = orders.get(activeTask.orderId);
                    orderRuntime.complete(activeTask.nodeId, tick, scenario.menus.get(orderRuntime.plan.menuId));
                    logs.add("tick " + tick + ": complete " + activeTask.orderId + "/" + activeTask.nodeId);
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
                                orderRuntime.completedNodeIds.size(),
                                menuDef.nodes.size()
                        );
                    })
                    .toList();

            int completedOrders = (int) outcomes.stream().filter(OrderOutcome::completed).count();
            double averageCompletion = outcomes.stream()
                    .filter(OrderOutcome::completed)
                    .mapToInt(outcome -> outcome.completionTick)
                    .average()
                    .orElse(Double.NaN);

            return new Result(
                    scenario.chapterName,
                    scenario.name,
                    policy.name(),
                    scenario.tickBudget,
                    scenario.orders.size(),
                    completedOrders,
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
            if (assignment == null) {
                return null;
            }
            return candidates.stream()
                    .filter(candidate -> candidate.orderId.equals(assignment.orderId)
                            && candidate.nodeId.equals(assignment.nodeId))
                    .findFirst()
                    .orElse(null);
        }
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

        private boolean isCompleted(MenuDef menuDef) {
            return completionTick != null || completedNodeIds.size() == menuDef.nodes.size();
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
            int completedNodeCount,
            int totalNodeCount
    ) {
        private boolean completed() {
            return completionTick != null;
        }
    }

    private static final class Result {
        private final String chapterName;
        private final String scenarioName;
        private final String policyName;
        private final int tickBudget;
        private final int totalOrders;
        private final int completedOrders;
        private final double averageCompletionTick;
        private final List<OrderOutcome> outcomes;
        private final List<String> logs;

        private Result(String chapterName, String scenarioName, String policyName, int tickBudget,
                       int totalOrders, int completedOrders, double averageCompletionTick,
                       List<OrderOutcome> outcomes, List<String> logs) {
            this.chapterName = chapterName;
            this.scenarioName = scenarioName;
            this.policyName = policyName;
            this.tickBudget = tickBudget;
            this.totalOrders = totalOrders;
            this.completedOrders = completedOrders;
            this.averageCompletionTick = averageCompletionTick;
            this.outcomes = outcomes;
            this.logs = logs;
        }

        private String key() {
            return chapterName + "::" + policyName;
        }

        private String summaryLine() {
            String avg = Double.isNaN(averageCompletionTick)
                    ? "-"
                    : String.format(Locale.US, "%.2f", averageCompletionTick);
            String completedOrderIds = outcomes.stream()
                    .filter(OrderOutcome::completed)
                    .map(OrderOutcome::orderId)
                    .collect(Collectors.joining(","));
            if (completedOrderIds.isBlank()) {
                completedOrderIds = "-";
            }

            return String.format(Locale.US,
                    "  %-24s completed=%d/%d avgCompletion=%s completedOrders=%s",
                    policyName, completedOrders, totalOrders, avg, completedOrderIds);
        }
    }
}
