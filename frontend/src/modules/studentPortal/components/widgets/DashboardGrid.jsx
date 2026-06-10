import { useEffect } from "react";
import {
  DndContext, closestCenter, PointerSensor, KeyboardSensor, useSensor, useSensors,
} from "@dnd-kit/core";
import {
  SortableContext, arrayMove, useSortable, rectSortingStrategy, sortableKeyboardCoordinates,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical, Eye, EyeOff, Settings2, RotateCcw, X } from "lucide-react";
import { useTranslation } from "react-i18next";
import useDashboardLayoutStore, { DEFAULT_WIDGET_ORDER } from "../../../../core/stores/useDashboardLayoutStore";
import { WIDGET_REGISTRY } from "./DashboardWidgets";
import "../../styles/dashboardWidgets.css";

function SortableWidget({ id, span, customizing, children }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id, disabled: !customizing,
  });
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  };
  return (
    <div ref={setNodeRef} style={style} className={`dw-slot span-${span} ${customizing ? "is-customizing" : ""}`}>
      {customizing && (
        <button type="button" className="dw-drag-handle" {...attributes} {...listeners} aria-label="Reorder widget">
          <GripVertical size={16} />
        </button>
      )}
      {children}
    </div>
  );
}

function DashboardGrid() {
  const { t } = useTranslation();
  const { widgetOrder, hiddenWidgets, customizing, hydrate, setOrder, toggleHidden, setCustomizing, resetLayout } =
    useDashboardLayoutStore();

  useEffect(() => { hydrate(); }, [hydrate]);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const visible = widgetOrder.filter((k) => !hiddenWidgets.includes(k) && WIDGET_REGISTRY[k]);

  const onDragEnd = ({ active, over }) => {
    if (!over || active.id === over.id) return;
    const from = widgetOrder.indexOf(active.id);
    const to = widgetOrder.indexOf(over.id);
    if (from === -1 || to === -1) return;
    setOrder(arrayMove(widgetOrder, from, to));
  };

  return (
    <div className="dw-wrap">
      <div className="dw-toolbar">
        <button type="button" className={`dw-customize-btn ${customizing ? "active" : ""}`} onClick={() => setCustomizing(!customizing)}>
          {customizing ? <X size={15} /> : <Settings2 size={15} />}
          {customizing ? t("dashboard.done", { defaultValue: "Done" }) : t("dashboard.customize", { defaultValue: "Customize" })}
        </button>
        {customizing && (
          <button type="button" className="dw-reset-btn" onClick={resetLayout}>
            <RotateCcw size={14} /> {t("dashboard.reset_layout", { defaultValue: "Reset" })}
          </button>
        )}
      </div>

      {customizing && (
        <div className="dw-visibility-panel">
          {DEFAULT_WIDGET_ORDER.map((key) => {
            const hidden = hiddenWidgets.includes(key);
            return (
              <button key={key} type="button" className={`dw-vis-chip ${hidden ? "hidden" : ""}`} onClick={() => toggleHidden(key)}>
                {hidden ? <EyeOff size={13} /> : <Eye size={13} />}
                {t(`dashboard.widget_${key}`, { defaultValue: key })}
              </button>
            );
          })}
        </div>
      )}

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={visible} strategy={rectSortingStrategy}>
          <div className="dw-grid">
            {visible.map((key, i) => {
              const { component: Widget, span } = WIDGET_REGISTRY[key];
              return (
                <SortableWidget key={key} id={key} span={span} customizing={customizing}>
                  <div className="dw-mount" style={{ animationDelay: `${i * 50}ms` }}>
                    <Widget />
                  </div>
                </SortableWidget>
              );
            })}
          </div>
        </SortableContext>
      </DndContext>
    </div>
  );
}

export default DashboardGrid;
