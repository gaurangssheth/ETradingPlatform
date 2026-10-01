import { toast } from "react-toastify";

const getToastId = (type: string, message: string): string =>
  `${type}:${message}`;

export const notifications = {
  success: (message: string): void => {
    toast.success(message, {
      toastId: getToastId("success", message),
    });
  },

  error: (message: string): void => {
    toast.error(message, {
      toastId: getToastId("error", message),
    });
  },

  warning: (message: string): void => {
    toast.warning(message, {
      toastId: getToastId("warning", message),
    });
  },

  info: (message: string): void => {
    toast.info(message, {
      toastId: getToastId("info", message),
    });
  },
};
